using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PrzewodnikSWPW.Web.Data;
using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.Services;

/// <summary>Błąd przypisany do pola formularza (<see cref="Pole"/> = "" — błąd całego formularza).</summary>
public sealed record BladPola(string Pole, string Komunikat);

public sealed record WynikOperacji(IReadOnlyList<BladPola> Bledy, string? Informacja = null)
{
    public bool Sukces => Bledy.Count == 0;
    public static WynikOperacji Ok(string? informacja = null) => new([], informacja);
    public static WynikOperacji Blad(string pole, string komunikat) => new([new BladPola(pole, komunikat)]);
}

public sealed record WpisAudytuWidok(DateTime Data, string Uzytkownik, string Encja, string Klucz, string Operacja, string? Stare, string? Nowe);

/// <summary>
/// Reguły panelu administratora. Unikalność i zależności są sprawdzane
/// przed zapisem, aby użytkownik dostał zrozumiały komunikat przy polu formularza, a nie błąd bazy.
/// Punktów, kierunków, sal i budynków nie usuwamy fizycznie — tylko CzyAktywny = false.
/// </summary>
/// <summary>Skutki usunięcia sali, piętra albo budynku — pokazywane redaktorowi, zanim potwierdzi.</summary>
/// <param name="Blokada">Powód, dla którego usunąć się nie da, albo <c>null</c>.</param>
public sealed record SkutkiUsuniecia(string Nazwa, string? Blokada, IReadOnlyList<string> Skutki);

/// <summary>Skutki usunięcia punktu ruchu — pokazywane redaktorowi, zanim potwierdzi.</summary>
/// <param name="WejscieGlowneBudynku">Kod budynku, którego wejściem głównym jest ten punkt; wtedy usunięcie jest zablokowane.</param>
/// <param name="PunktyZPrzejsciemTutaj">Kody punktów, z których prowadzi kierunek do usuwanego punktu.</param>
public sealed record SkutkiUsunieciaPunktu(
    string Kod,
    string Nazwa,
    string? WejscieGlowneBudynku,
    int KierunkiWychodzace,
    IReadOnlyList<string> PunktyZPrzejsciemTutaj,
    IReadOnlyList<string> SaleTracaceWejscie,
    int Zdjecia);

public sealed class AdministracjaService(IAdministracjaRepozytorium repo)
{
    // --- Budynki -------------------------------------------------------------------------------------

    public Task<List<Budynek>> Budynki(CancellationToken ct = default) =>
        repo.Zapytanie<Budynek>().AsNoTracking().Include(b => b.Pietra).OrderBy(b => b.Kod).ToListAsync(ct);

    public Task<Budynek?> PobierzBudynek(int id, CancellationToken ct = default) => repo.ZnajdzAsync<Budynek>(id, ct);

    public async Task<WynikOperacji> ZapiszBudynek(Budynek b, CancellationToken ct = default)
    {
        if (await repo.Zapytanie<Budynek>().AnyAsync(x => x.Kod == b.Kod && x.Id != b.Id, ct))
        {
            return WynikOperacji.Blad(nameof(Budynek.Kod), $"Kod „{b.Kod}” ma już inny budynek. Wybierz inny kod.");
        }
        // Wejście główne musi leżeć w tym budynku. Nowy budynek nie ma jeszcze punktów — wejście ustawia się później.
        if (b.PunktWejsciaGlownegoId is int wejscie
            && !await repo.Zapytanie<PunktRuchu>().AnyAsync(p => p.Id == wejscie && p.Pietro.BudynekId == b.Id, ct))
        {
            return WynikOperacji.Blad(nameof(Budynek.PunktWejsciaGlownegoId), "Wejście główne musi być punktem ruchu tego budynku.");
        }
        return await Zapisz(b, ct);
    }

    /// <summary>Punkty ruchu budynku — do listy wyboru wejścia głównego.</summary>
    public Task<List<PunktRuchu>> PunktyBudynku(int budynekId, CancellationToken ct = default) =>
        repo.Zapytanie<PunktRuchu>().AsNoTracking().Where(p => p.Pietro.BudynekId == budynekId).OrderBy(p => p.Kod).ToListAsync(ct);

    public async Task<SkutkiUsuniecia?> SkutkiUsunieciaBudynku(int id, CancellationToken ct = default)
    {
        var b = await repo.Zapytanie<Budynek>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b is null) return null;

        var pietra = await repo.Zapytanie<Pietro>().AsNoTracking().CountAsync(p => p.BudynekId == id, ct);
        var punkty = await repo.Zapytanie<PunktRuchu>().AsNoTracking().Where(p => p.Pietro.BudynekId == id).Select(p => p.Id).ToListAsync(ct);
        var sale = await repo.Zapytanie<Sala>().AsNoTracking().Where(s => s.Pietro.BudynekId == id).Select(s => s.Id).ToListAsync(ct);
        var skutki = new List<string>();
        if (pietra > 0) skutki.Add($"Znikną piętra budynku: {pietra}.");
        if (punkty.Count > 0) skutki.Add($"Znikną punkty ruchu: {punkty.Count}.");
        if (sale.Count > 0) skutki.Add($"Znikną sale: {sale.Count}.");
        skutki.AddRange(await OpiszSkutkiAsync(punkty, sale, ct));
        return new SkutkiUsuniecia($"budynek {b.Kod} — {b.Nazwa}", null, skutki);
    }

    /// <summary>Usuwa budynek z całą zawartością: piętrami, salami, punktami ruchu, kierunkami i zdjęciami.</summary>
    public async Task<(WynikOperacji Wynik, IReadOnlyList<string> PlikiZdjec)> UsunBudynek(int id, CancellationToken ct = default)
    {
        var budynek = await repo.ZnajdzAsync<Budynek>(id, ct);
        if (budynek is null) return (WynikOperacji.Blad("", "Nie znaleziono budynku."), []);

        await using var transakcja = await repo.RozpocznijTransakcjeAsync(ct);
        // Budynek wskazuje na swój punkt wejścia, a punkt (przez piętro) na budynek — najpierw rozłączamy.
        budynek.PunktWejsciaGlownegoId = null;
        await repo.ZapiszAsync(ct);

        var pliki = await UsunZawartoscAsync(
            await repo.Zapytanie<PunktRuchu>().Where(p => p.Pietro.BudynekId == id).Select(p => p.Id).ToListAsync(ct),
            await repo.Zapytanie<Sala>().Where(s => s.Pietro.BudynekId == id).Select(s => s.Id).ToListAsync(ct), ct);
        foreach (var pietro in await repo.Zapytanie<Pietro>().Where(p => p.BudynekId == id).ToListAsync(ct))
        {
            repo.Usun(pietro);
        }
        repo.Usun(budynek);
        await repo.ZapiszAsync(ct);
        await transakcja.CommitAsync(ct);
        return (WynikOperacji.Ok(), pliki);
    }

    public Task<WynikOperacji> UstawAktywnoscBudynku(int id, bool aktywny, CancellationToken ct = default) =>
        UstawAktywnosc<Budynek>(id, b => b.CzyAktywny = aktywny, ct);

    // --- Piętra ----------------------------------------------------------------------------------------

    public Task<List<Pietro>> Pietra(CancellationToken ct = default) =>
        repo.Zapytanie<Pietro>().AsNoTracking()
            .Include(p => p.Budynek).Include(p => p.Sale).Include(p => p.PunktyRuchu)
            .OrderBy(p => p.Budynek.Kod).ThenBy(p => p.Numer).ToListAsync(ct);

    public Task<Pietro?> PobierzPietro(int id, CancellationToken ct = default) =>
        repo.Zapytanie<Pietro>().Include(p => p.Budynek).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<WynikOperacji> ZapiszPietro(Pietro p, CancellationToken ct = default)
    {
        if (!await repo.Zapytanie<Budynek>().AnyAsync(b => b.Id == p.BudynekId, ct))
        {
            return WynikOperacji.Blad(nameof(Pietro.BudynekId), "Wybierz budynek z listy.");
        }
        if (await repo.Zapytanie<Pietro>().AnyAsync(x => x.BudynekId == p.BudynekId && x.Numer == p.Numer && x.Id != p.Id, ct))
        {
            return WynikOperacji.Blad(nameof(Pietro.Numer), $"Ten budynek ma już piętro numer {p.Numer}.");
        }
        return await Zapisz(p, ct);
    }

    /// <summary>Piętro usuwamy tylko puste — z salami lub punktami ruchu usunięcie skasowałoby część grafu.</summary>
    public async Task<SkutkiUsuniecia?> SkutkiUsunieciaPietra(int id, CancellationToken ct = default)
    {
        var p = await repo.Zapytanie<Pietro>().AsNoTracking().Include(x => x.Budynek).Include(x => x.Sale).Include(x => x.PunktyRuchu)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return null;

        var punkty = p.PunktyRuchu.Select(x => x.Id).ToList();
        var skutki = new List<string>();
        if (punkty.Count > 0) skutki.Add($"Znikną punkty ruchu tego piętra: {punkty.Count}.");
        if (p.Sale.Count > 0) skutki.Add($"Znikną sale tego piętra: {p.Sale.Count} ({string.Join(", ", p.Sale.OrderBy(s => s.Symbol).Select(s => s.Symbol))}).");
        skutki.AddRange(await OpiszSkutkiAsync(punkty, p.Sale.Select(s => s.Id).ToList(), ct));

        var blokada = p.Budynek.PunktWejsciaGlownegoId is int wejscie && punkty.Contains(wejscie)
            ? $"Na tym piętrze jest wejście główne budynku {p.Budynek.Kod}. Najpierw wybierz inne wejście główne w edycji budynku " +
              "albo usuń cały budynek."
            : null;
        return new SkutkiUsuniecia($"piętro „{p.Nazwa}” w budynku {p.Budynek.Kod}", blokada, skutki);
    }

    /// <summary>Usuwa piętro razem z jego salami, punktami ruchu i wszystkim, co na nie wskazuje.</summary>
    public async Task<(WynikOperacji Wynik, IReadOnlyList<string> PlikiZdjec)> UsunPietro(int id, CancellationToken ct = default)
    {
        if (await SkutkiUsunieciaPietra(id, ct) is not { } skutki) return (WynikOperacji.Blad("", "Nie znaleziono piętra."), []);
        if (skutki.Blokada is not null) return (WynikOperacji.Blad("", skutki.Blokada), []);

        await using var transakcja = await repo.RozpocznijTransakcjeAsync(ct);
        var pliki = await UsunZawartoscAsync(
            await repo.Zapytanie<PunktRuchu>().Where(x => x.PietroId == id).Select(x => x.Id).ToListAsync(ct),
            await repo.Zapytanie<Sala>().Where(x => x.PietroId == id).Select(x => x.Id).ToListAsync(ct), ct);
        repo.Usun((await repo.ZnajdzAsync<Pietro>(id, ct))!);
        await repo.ZapiszAsync(ct);
        await transakcja.CommitAsync(ct);
        return (WynikOperacji.Ok(), pliki);
    }

    // --- Sale ------------------------------------------------------------------------------------------

    public Task<List<Sala>> Sale(CancellationToken ct = default) =>
        repo.Zapytanie<Sala>().AsNoTracking()
            .Include(s => s.Pietro).ThenInclude(p => p.Budynek).Include(s => s.TypSali).Include(s => s.PunktWejsciowy)
            .OrderBy(s => s.Pietro.Budynek.Kod).ThenBy(s => s.Pietro.Numer).ThenBy(s => s.Symbol).ToListAsync(ct);

    public Task<Sala?> PobierzSale(int id, CancellationToken ct = default) =>
        repo.Zapytanie<Sala>().Include(s => s.Pietro).ThenInclude(p => p.Budynek).FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<WynikOperacji> ZapiszSale(Sala s, CancellationToken ct = default)
    {
        var pietro = await repo.Zapytanie<Pietro>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == s.PietroId, ct);
        if (pietro is null) return WynikOperacji.Blad(nameof(Sala.PietroId), "Wybierz piętro z listy.");
        if (!await repo.Zapytanie<TypSali>().AnyAsync(t => t.Id == s.TypSaliId, ct))
        {
            return WynikOperacji.Blad(nameof(Sala.TypSaliId), "Wybierz typ sali z listy.");
        }
        if (await repo.Zapytanie<Sala>().AnyAsync(x => x.PietroId == s.PietroId && x.Symbol == s.Symbol && x.Id != s.Id, ct))
        {
            return WynikOperacji.Blad(nameof(Sala.Symbol), $"Na tym piętrze jest już sala o symbolu „{s.Symbol}”.");
        }
        if (s.PunktWejsciowyId is int punktId
            && !await repo.Zapytanie<PunktRuchu>().AnyAsync(p => p.Id == punktId && p.Pietro.BudynekId == pietro.BudynekId, ct))
        {
            return WynikOperacji.Blad(nameof(Sala.PunktWejsciowyId), "Punkt wejściowy musi leżeć w tym samym budynku co sala.");
        }
        return await Zapisz(s, ct);
    }

    public async Task<SkutkiUsuniecia?> SkutkiUsunieciaSali(int id, CancellationToken ct = default)
    {
        var s = await repo.Zapytanie<Sala>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return s is null ? null : new SkutkiUsuniecia($"sala {s.Symbol} — {s.Nazwa}", null, await OpiszSkutkiAsync([], [id], ct));
    }

    /// <summary>Usuwa salę razem z kierunkami „drzwi”, które do niej prowadzą, i jej zdjęciami.</summary>
    public async Task<(WynikOperacji Wynik, IReadOnlyList<string> PlikiZdjec)> UsunSale(int id, CancellationToken ct = default)
    {
        if (await repo.ZnajdzAsync<Sala>(id, ct) is null) return (WynikOperacji.Blad("", "Nie znaleziono sali."), []);

        await using var transakcja = await repo.RozpocznijTransakcjeAsync(ct);
        var pliki = await UsunZawartoscAsync([], [id], ct);
        await transakcja.CommitAsync(ct);
        return (WynikOperacji.Ok(), pliki);
    }

    public Task<WynikOperacji> UstawAktywnoscSali(int id, bool aktywna, CancellationToken ct = default) =>
        UstawAktywnosc<Sala>(id, s => s.CzyAktywna = aktywna, ct);

    // --- Słowniki: TypSali, TypPunktu, Udogodnienie --------------------------------------------------

    public Task<List<T>> Slownik<T>(CancellationToken ct = default) where T : class, ISlownik =>
        repo.Zapytanie<T>().AsNoTracking().OrderBy(x => x.Nazwa).ToListAsync(ct);

    public Task<T?> PozycjaSlownika<T>(int id, CancellationToken ct = default) where T : class, ISlownik => repo.ZnajdzAsync<T>(id, ct);

    public async Task<WynikOperacji> ZapiszSlownik<T>(T pozycja, CancellationToken ct = default) where T : class, ISlownik
    {
        if (await repo.Zapytanie<T>().AnyAsync(x => x.Nazwa == pozycja.Nazwa && x.Id != pozycja.Id, ct))
        {
            return WynikOperacji.Blad(nameof(ISlownik.Nazwa), $"Pozycja o nazwie „{pozycja.Nazwa}” już istnieje.");
        }
        return await Zapisz(pozycja, ct);
    }

    /// <summary>Liczba rekordów korzystających z pozycji słownika — pozycji w użyciu nie usuwamy.</summary>
    public async Task<int> UzyciaSlownika<T>(int id, CancellationToken ct = default) where T : class, ISlownik => typeof(T).Name switch
    {
        nameof(TypSali) => await repo.Zapytanie<Sala>().CountAsync(s => s.TypSaliId == id, ct),
        nameof(TypPunktu) => await repo.Zapytanie<PunktRuchu>().CountAsync(p => p.TypPunktuId == id, ct),
        nameof(Udogodnienie) => await repo.Zapytanie<PunktUdogodnienie>().CountAsync(u => u.UdogodnienieId == id, ct)
                              + await repo.Zapytanie<SalaUdogodnienie>().CountAsync(u => u.UdogodnienieId == id, ct),
        _ => throw new NotSupportedException(typeof(T).Name),
    };

    public async Task<WynikOperacji> UsunSlownik<T>(int id, CancellationToken ct = default) where T : class, ISlownik
    {
        var pozycja = await repo.ZnajdzAsync<T>(id, ct);
        if (pozycja is null) return WynikOperacji.Blad("", "Nie znaleziono pozycji.");
        var uzycia = await UzyciaSlownika<T>(id, ct);
        if (uzycia > 0)
        {
            return WynikOperacji.Blad("", $"Nie można usunąć „{pozycja.Nazwa}”: używa jej {uzycia} {Odmiana.PoLiczbie(uzycia, "rekord", "rekordy", "rekordów")}.");
        }
        repo.Usun(pozycja);
        await repo.ZapiszAsync(ct);
        return WynikOperacji.Ok();
    }

    // --- Punkty ruchu ----------------------------------------------------------------------------------

    public Task<List<PunktRuchu>> Punkty(CancellationToken ct = default) =>
        repo.Zapytanie<PunktRuchu>().AsNoTracking()
            .Include(p => p.Pietro).ThenInclude(p => p.Budynek).Include(p => p.TypPunktu).Include(p => p.KierunkiWychodzace)
            .OrderBy(p => p.Kod).ToListAsync(ct);

    public Task<PunktRuchu?> PobierzPunkt(int id, CancellationToken ct = default) =>
        repo.Zapytanie<PunktRuchu>().Include(p => p.Pietro).ThenInclude(p => p.Budynek).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<WynikOperacji> ZapiszPunkt(PunktRuchu p, CancellationToken ct = default)
    {
        if (!await repo.Zapytanie<Pietro>().AnyAsync(x => x.Id == p.PietroId, ct))
            return WynikOperacji.Blad(nameof(PunktRuchu.PietroId), "Wybierz piętro z listy.");
        if (!await repo.Zapytanie<TypPunktu>().AnyAsync(x => x.Id == p.TypPunktuId, ct))
            return WynikOperacji.Blad(nameof(PunktRuchu.TypPunktuId), "Wybierz typ punktu z listy.");
        if (await repo.Zapytanie<PunktRuchu>().AnyAsync(x => x.Kod == p.Kod && x.Id != p.Id, ct))
            return WynikOperacji.Blad(nameof(PunktRuchu.Kod), $"Kod „{p.Kod}” ma już inny punkt ruchu.");
        return await Zapisz(p, ct);
    }

    public Task<WynikOperacji> UstawAktywnoscPunktu(int id, bool aktywny, CancellationToken ct = default) =>
        UstawAktywnosc<PunktRuchu>(id, p => p.CzyAktywny = aktywny, ct);

    /// <summary>Co stanie się po usunięciu punktu — do pokazania na stronie potwierdzenia; <c>null</c>, gdy punktu nie ma.</summary>
    public async Task<SkutkiUsunieciaPunktu?> SkutkiUsunieciaPunktu(int id, CancellationToken ct = default)
    {
        var punkt = await repo.Zapytanie<PunktRuchu>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (punkt is null) return null;

        var kody = await repo.Zapytanie<PunktRuchu>().AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Kod, ct);
        return new SkutkiUsunieciaPunktu(
            punkt.Kod,
            punkt.Nazwa,
            await repo.Zapytanie<Budynek>().AsNoTracking().Where(b => b.PunktWejsciaGlownegoId == id).Select(b => b.Kod).FirstOrDefaultAsync(ct),
            await repo.Zapytanie<Kierunek>().AsNoTracking().CountAsync(k => k.PunktZrodlowyId == id, ct),
            (await repo.Zapytanie<Kierunek>().AsNoTracking().Where(k => k.PunktDocelowyId == id).Select(k => k.PunktZrodlowyId).ToListAsync(ct))
                .Select(z => kody[z]).Distinct().Order().ToList(),
            await repo.Zapytanie<Sala>().AsNoTracking().Where(s => s.PunktWejsciowyId == id).OrderBy(s => s.Symbol).Select(s => s.Symbol).ToListAsync(ct),
            await repo.Zapytanie<Zdjecie>().AsNoTracking().CountAsync(z => z.PunktRuchuId == id, ct));
    }

    /// <summary>
    /// Usuwa punkt razem z tym, co bez niego nie ma sensu: kierunkami wychodzącymi i przychodzącymi,
    /// jego zdjęciami i utrudnieniami. Sale, które miały tu wejście, zostają bez punktu wejściowego —
    /// walidator grafu je wskaże. Wejścia głównego budynku usunąć się nie da: najpierw trzeba wybrać inne.
    /// Zwraca ścieżki plików zdjęć do skasowania z dysku (po zatwierdzeniu transakcji).
    /// </summary>
    public async Task<(WynikOperacji Wynik, IReadOnlyList<string> PlikiZdjec)> UsunPunkt(int id, CancellationToken ct = default)
    {
        var punkt = await repo.ZnajdzAsync<PunktRuchu>(id, ct);
        if (punkt is null) return (WynikOperacji.Blad("", "Nie znaleziono punktu."), []);

        if (await repo.Zapytanie<Budynek>().Where(b => b.PunktWejsciaGlownegoId == id).Select(b => b.Kod).FirstOrDefaultAsync(ct) is { } kodBudynku)
        {
            return (WynikOperacji.Blad("", $"Punkt {punkt.Kod} jest wejściem głównym budynku {kodBudynku}. " +
                                           "Najpierw wybierz inne wejście główne w edycji budynku."), []);
        }

        await using var transakcja = await repo.RozpocznijTransakcjeAsync(ct);
        var pliki = await UsunZawartoscAsync([id], [], ct);
        await transakcja.CommitAsync(ct);
        return (WynikOperacji.Ok(), pliki);
    }

    /// <summary>
    /// Wspólne usuwanie punktów ruchu i sal razem z tym, co na nie wskazuje. Baza niczego nie kasuje sama
    /// (klucze obce bez kaskady), więc kolejność jest jawna:
    /// 1) kierunki wychodzące z punktów, prowadzące do nich i drzwi do sal,
    /// 2) sale spoza usuwanych, które miały wejście w usuwanym punkcie — zostają, bez punktu wejściowego,
    /// 3) utrudnienia punktów, zapisane zapytania o trasę (zostają, bez wskazania sali), zdjęcia,
    /// 4) same sale i punkty.
    /// Zwraca ścieżki plików zdjęć do skasowania z dysku po zatwierdzeniu transakcji.
    /// </summary>
    private async Task<IReadOnlyList<string>> UsunZawartoscAsync(IReadOnlyCollection<int> punkty, IReadOnlyCollection<int> sale, CancellationToken ct)
    {
        var kierunki = await repo.Zapytanie<Kierunek>()
            .Where(k => punkty.Contains(k.PunktZrodlowyId)
                        || (k.PunktDocelowyId != null && punkty.Contains(k.PunktDocelowyId.Value))
                        || (k.SalaDocelowaId != null && sale.Contains(k.SalaDocelowaId.Value)))
            .Select(k => k.Id).ToListAsync(ct);
        await UsunKierunkiAsync(kierunki, ct);

        foreach (var sala in await repo.Zapytanie<Sala>()
                     .Where(s => !sale.Contains(s.Id) && s.PunktWejsciowyId != null && punkty.Contains(s.PunktWejsciowyId.Value)).ToListAsync(ct))
        {
            sala.PunktWejsciowyId = null;
        }

        foreach (var utrudnienie in await repo.Zapytanie<Utrudnienie>()
                     .Where(u => u.PunktRuchuId != null && punkty.Contains(u.PunktRuchuId.Value)).ToListAsync(ct))
        {
            repo.Usun(utrudnienie);
        }

        foreach (var zapytanie in await repo.Zapytanie<ZapytanieTrasy>()
                     .Where(z => (z.SalaZId != null && sale.Contains(z.SalaZId.Value)) || (z.SalaDoId != null && sale.Contains(z.SalaDoId.Value))).ToListAsync(ct))
        {
            if (zapytanie.SalaZId is int z && sale.Contains(z)) zapytanie.SalaZId = null;
            if (zapytanie.SalaDoId is int d && sale.Contains(d)) zapytanie.SalaDoId = null;
        }

        var zdjecia = await repo.Zapytanie<Zdjecie>()
            .Where(z => (z.PunktRuchuId != null && punkty.Contains(z.PunktRuchuId.Value)) || (z.SalaId != null && sale.Contains(z.SalaId.Value)))
            .ToListAsync(ct);
        foreach (var zdjecie in zdjecia)
        {
            repo.Usun(zdjecie);
        }
        await repo.ZapiszAsync(ct);

        // Najpierw sale: wskazują na punkt wejściowy, który też może być na liście.
        foreach (var sala in await repo.Zapytanie<Sala>().Where(s => sale.Contains(s.Id)).ToListAsync(ct))
        {
            repo.Usun(sala);
        }
        await repo.ZapiszAsync(ct);

        foreach (var punkt in await repo.Zapytanie<PunktRuchu>().Where(p => punkty.Contains(p.Id)).ToListAsync(ct))
        {
            repo.Usun(punkt);
        }
        await repo.ZapiszAsync(ct);

        return zdjecia.Select(z => z.SciezkaPliku).ToList();
    }

    /// <summary>Skutki usunięcia punktów i sal słowami — wspólne dla sali, piętra i budynku.</summary>
    private async Task<List<string>> OpiszSkutkiAsync(IReadOnlyCollection<int> punkty, IReadOnlyCollection<int> sale, CancellationToken ct)
    {
        var skutki = new List<string>();
        var kierunki = await repo.Zapytanie<Kierunek>().AsNoTracking()
            .CountAsync(k => punkty.Contains(k.PunktZrodlowyId)
                             || (k.PunktDocelowyId != null && punkty.Contains(k.PunktDocelowyId.Value))
                             || (k.SalaDocelowaId != null && sale.Contains(k.SalaDocelowaId.Value)), ct);
        if (kierunki > 0) skutki.Add($"Znikną kierunki z nimi związane: {kierunki}.");

        var bezWejscia = await repo.Zapytanie<Sala>().AsNoTracking()
            .Where(s => !sale.Contains(s.Id) && s.PunktWejsciowyId != null && punkty.Contains(s.PunktWejsciowyId.Value))
            .OrderBy(s => s.Symbol).Select(s => s.Symbol).ToListAsync(ct);
        if (bezWejscia.Count > 0)
        {
            skutki.Add($"Sale na innych piętrach zostaną bez punktu wejściowego: {string.Join(", ", bezWejscia)}. " +
                       "Dopóki nie wskażesz im nowego, nie da się do nich wyznaczyć trasy.");
        }

        var zdjecia = await repo.Zapytanie<Zdjecie>().AsNoTracking()
            .CountAsync(z => (z.PunktRuchuId != null && punkty.Contains(z.PunktRuchuId.Value)) || (z.SalaId != null && sale.Contains(z.SalaId.Value)), ct);
        if (zdjecia > 0) skutki.Add($"Znikną zdjęcia razem z plikami: {zdjecia}.");
        return skutki;
    }

    // --- Kierunki (krawędzie grafu) --------------------------------------------------------------------

    public Task<List<Kierunek>> KierunkiPunktu(int punktId, CancellationToken ct = default) =>
        repo.Zapytanie<Kierunek>().AsNoTracking()
            .Include(k => k.PunktDocelowy).Include(k => k.SalaDocelowa)
            .Where(k => k.PunktZrodlowyId == punktId).OrderBy(k => k.Azymut).ToListAsync(ct);

    public Task<Kierunek?> PobierzKierunek(int id, CancellationToken ct = default) =>
        repo.Zapytanie<Kierunek>().Include(k => k.PunktZrodlowy).Include(k => k.KierunekPowrotny).FirstOrDefaultAsync(k => k.Id == id, ct);

    /// <summary>
    /// Dodaje kierunek A→B i — domyślnie — powrotny B→A z tą samą wagą i azymutem odwróconym o 180°
    /// (połowa pracy mniej i żadnej krawędzi bez pary). Oba w jednej transakcji,
    /// wzajemnie powiązane przez KierunekPowrotnyId. Gdy B→A już istnieje, zostaje tylko powiązany.
    /// </summary>
    public async Task<WynikOperacji> DodajKierunek(Kierunek k, bool utworzPowrotny, string? opisPowrotny, CancellationToken ct = default)
    {
        var bledy = await SprawdzKierunek(k, ct);
        var azymutPowrotny = Azymuty.Normalizuj(k.Azymut + 180);
        Kierunek? istniejacyPowrotny = null;

        if (k.PunktDocelowyId is int b)
        {
            istniejacyPowrotny = await repo.Zapytanie<Kierunek>()
                .FirstOrDefaultAsync(x => x.PunktZrodlowyId == b && x.Azymut == azymutPowrotny, ct);

            if (utworzPowrotny && istniejacyPowrotny is not null && istniejacyPowrotny.PunktDocelowyId != k.PunktZrodlowyId)
            {
                bledy.Add(new BladPola("UtworzPowrotny",
                    $"Punkt docelowy ma już kierunek o azymucie {azymutPowrotny}°, prowadzący gdzie indziej — kierunek powrotny nie może powstać. " +
                    "Sprawdź azymut albo odznacz tworzenie kierunku powrotnego."));
            }
        }
        if (bledy.Count > 0) return new WynikOperacji(bledy);

        await using var transakcja = await repo.RozpocznijTransakcjeAsync(ct);
        repo.Dodaj(k);
        await repo.ZapiszAsync(ct);

        string? informacja = null;
        if (k.PunktDocelowyId is int docelowy)
        {
            var powrotny = istniejacyPowrotny;
            if (powrotny is null && utworzPowrotny)
            {
                powrotny = new Kierunek
                {
                    PunktZrodlowyId = docelowy,
                    PunktDocelowyId = k.PunktZrodlowyId,
                    Azymut = azymutPowrotny,
                    Waga = k.Waga,
                    RodzajPrzejscia = k.RodzajPrzejscia,
                    CzyAktywny = k.CzyAktywny,
                    CzyDostepnyBezSchodow = k.CzyDostepnyBezSchodow,
                    OpisPrzejscia = string.IsNullOrWhiteSpace(opisPowrotny) ? null : opisPowrotny.Trim(),
                };
                repo.Dodaj(powrotny);
                await repo.ZapiszAsync(ct);
                informacja = $"Utworzono też kierunek powrotny o azymucie {azymutPowrotny}°.";
            }
            else if (powrotny is not null)
            {
                informacja = "Kierunek powiązano z istniejącym kierunkiem powrotnym.";
            }

            if (powrotny is not null)
            {
                k.KierunekPowrotnyId = powrotny.Id;
                powrotny.KierunekPowrotnyId = k.Id;
                await repo.ZapiszAsync(ct);
            }
        }
        else
        {
            informacja = "Kierunek prowadzi do sali — kierunku powrotnego z sali nie tworzymy.";
        }

        await transakcja.CommitAsync(ct);
        return WynikOperacji.Ok(informacja);
    }

    public async Task<WynikOperacji> ZapiszKierunek(Kierunek k, CancellationToken ct = default)
    {
        var bledy = await SprawdzKierunek(k, ct);
        return bledy.Count > 0 ? new WynikOperacji(bledy) : await Zapisz(k, ct);
    }

    /// <summary>
    /// Usuwa kierunek, a na życzenie także jego kierunek powrotny. Bez tej opcji kierunek powrotny zostaje
    /// bez pary — walidator grafu zgłosi to jako błąd, bo użytkownik przejdzie tamtędy, ale nie wróci.
    /// </summary>
    public async Task<WynikOperacji> UsunKierunek(int id, bool takzePowrotny, CancellationToken ct = default)
    {
        var kierunek = await repo.ZnajdzAsync<Kierunek>(id, ct);
        if (kierunek is null) return WynikOperacji.Blad("", "Nie znaleziono kierunku.");

        var doUsuniecia = new List<int> { id };
        if (takzePowrotny && kierunek.KierunekPowrotnyId is int powrotny)
        {
            doUsuniecia.Add(powrotny);
        }

        await using var transakcja = await repo.RozpocznijTransakcjeAsync(ct);
        await UsunKierunkiAsync(doUsuniecia, ct);
        await transakcja.CommitAsync(ct);
        return WynikOperacji.Ok();
    }

    /// <summary>
    /// Usuwa kierunki razem z tym, co na nie wskazuje. Klucze obce kierunku nie mają kaskady w bazie
    /// (dwie ścieżki do punktu ruchu to błąd SQL Server 1785), więc powiązania rozłączamy tutaj, jawnie:
    /// 1) wskazania „kierunek powrotny” — inaczej para kierunków wskazujących na siebie nie dałaby się usunąć,
    /// 2) utrudnienia i obszary zdjęć przypisane do kierunku,
    /// 3) same kierunki.
    /// </summary>
    private async Task UsunKierunkiAsync(IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return;

        var powiazane = await repo.Zapytanie<Kierunek>()
            .Where(k => ids.Contains(k.Id) || (k.KierunekPowrotnyId != null && ids.Contains(k.KierunekPowrotnyId.Value)))
            .ToListAsync(ct);
        foreach (var kierunek in powiazane)
        {
            kierunek.KierunekPowrotnyId = null;
        }
        await repo.ZapiszAsync(ct);

        foreach (var utrudnienie in await repo.Zapytanie<Utrudnienie>().Where(u => u.KierunekId != null && ids.Contains(u.KierunekId.Value)).ToListAsync(ct))
        {
            repo.Usun(utrudnienie);
        }
        foreach (var obszar in await repo.Zapytanie<ObszarAktywny>().Where(o => ids.Contains(o.KierunekId)).ToListAsync(ct))
        {
            repo.Usun(obszar);
        }
        foreach (var kierunek in powiazane.Where(k => ids.Contains(k.Id)))
        {
            repo.Usun(kierunek);
        }
        await repo.ZapiszAsync(ct);
    }

    /// <summary>Dezaktywacja: kierunek zostaje jako „brak przejścia” z opisem orientacyjnym.</summary>
    public async Task<WynikOperacji> UstawAktywnoscKierunku(int id, bool aktywny, bool takzePowrotny, CancellationToken ct = default)
    {
        var k = await repo.ZnajdzAsync<Kierunek>(id, ct);
        if (k is null) return WynikOperacji.Blad("", "Nie znaleziono kierunku.");
        k.CzyAktywny = aktywny;
        if (takzePowrotny && k.KierunekPowrotnyId is int p && await repo.ZnajdzAsync<Kierunek>(p, ct) is { } powrotny)
        {
            powrotny.CzyAktywny = aktywny;
        }
        await repo.ZapiszAsync(ct);
        return WynikOperacji.Ok();
    }

    private async Task<List<BladPola>> SprawdzKierunek(Kierunek k, CancellationToken ct)
    {
        var bledy = new List<BladPola>();
        if (!await repo.Zapytanie<PunktRuchu>().AnyAsync(p => p.Id == k.PunktZrodlowyId, ct))
            bledy.Add(new BladPola(nameof(Kierunek.PunktZrodlowyId), "Wybierz punkt źródłowy z listy."));
        if (await repo.Zapytanie<Kierunek>().AnyAsync(x => x.PunktZrodlowyId == k.PunktZrodlowyId && x.Azymut == k.Azymut && x.Id != k.Id, ct))
            bledy.Add(new BladPola(nameof(Kierunek.Azymut),
                $"Z tego punktu wychodzi już kierunek o azymucie {k.Azymut}°. Z jednego punktu w danym azymucie może prowadzić tylko jeden kierunek."));
        if (k.PunktDocelowyId is int b && !await repo.Zapytanie<PunktRuchu>().AnyAsync(p => p.Id == b, ct))
            bledy.Add(new BladPola(nameof(Kierunek.PunktDocelowyId), "Wybierz punkt docelowy z listy."));
        if (k.SalaDocelowaId is int s && !await repo.Zapytanie<Sala>().AnyAsync(x => x.Id == s, ct))
            bledy.Add(new BladPola(nameof(Kierunek.SalaDocelowaId), "Wybierz salę docelową z listy."));
        return bledy;
    }

    // --- Rejestr zmian -----------------------------------------------------------------------------

    public async Task<List<WpisAudytuWidok>> OstatnieWpisyAudytu(int ile, CancellationToken ct = default)
    {
        var wpisy = await repo.Zapytanie<WpisAudytu>().AsNoTracking().OrderByDescending(w => w.Id).Take(ile).ToListAsync(ct);
        var ids = wpisy.Where(w => w.UzytkownikId != null).Select(w => w.UzytkownikId!).Distinct().ToList();
        var nazwy = await repo.Zapytanie<IdentityUser>().AsNoTracking().Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email ?? u.UserName ?? u.Id, ct);
        return wpisy.Select(w => new WpisAudytuWidok(w.DataOperacji,
            w.UzytkownikId is null ? "system" : nazwy.GetValueOrDefault(w.UzytkownikId, w.UzytkownikId),
            w.Encja, w.KluczEncji, w.Operacja, w.WartosciStare, w.WartosciNowe)).ToList();
    }

    // --- wspólne ---------------------------------------------------------------------------------------

    private async Task<WynikOperacji> Zapisz<T>(T encja, CancellationToken ct) where T : class
    {
        if (EncjaJestNowa(encja))
        {
            repo.Dodaj(encja);
        }
        await repo.ZapiszAsync(ct);
        return WynikOperacji.Ok();
    }

    private static bool EncjaJestNowa(object encja) => encja.GetType().GetProperty("Id")?.GetValue(encja) is 0;

    private async Task<WynikOperacji> UstawAktywnosc<T>(int id, Action<T> ustaw, CancellationToken ct) where T : class
    {
        var encja = await repo.ZnajdzAsync<T>(id, ct);
        if (encja is null) return WynikOperacji.Blad("", "Nie znaleziono rekordu.");
        ustaw(encja);
        await repo.ZapiszAsync(ct);
        return WynikOperacji.Ok();
    }
}
