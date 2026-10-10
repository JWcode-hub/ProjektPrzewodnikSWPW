using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PrzewodnikSWPW.Web.Data;
using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.Services;

public sealed class DaneBudynkowOptions
{
    public const string Sekcja = "Przewodnik";

    /// <summary>Plik z danymi budynków, względem katalogu aplikacji. Jest w repozytorium — to on przenosi dane między komputerami.</summary>
    public string PlikDanych { get; set; } = "Dane/przewodnik.json";
}

// Zawartość pliku danych. Rekordy wskazują na siebie kodami i symbolami, nie identyfikatorami z bazy —
// identyfikatory są inne na każdym komputerze, a kod punktu i symbol sali nadaje redaktor.

public sealed record PlikDanych(int Wersja, List<SlownikDane> TypySal, List<SlownikDane> Udogodnienia, List<BudynekDane> Budynki);

public sealed record SlownikDane(string Nazwa, string? Opis, string? Ikona = null);

public sealed record BudynekDane(string Kod, string Nazwa, string? Adres, string? Opis, string? OpisDostepnosciArchitektonicznej,
    bool CzyMaWinde, bool CzyAktywny, string? PunktWejsciaGlownego, List<PietroDane> Pietra);

public sealed record PietroDane(int Numer, string Nazwa, string? Opis, List<PunktDane> Punkty, List<SalaDane> Sale);

public sealed record PunktDane(string Kod, string Typ, string Nazwa, string? Opis, string? OpisGlosowy, int AzymutDomyslny,
    decimal? X, decimal? Y, bool CzyAktywny, List<UdogodnienieDane> Udogodnienia, List<ZdjecieDane> Zdjecia, List<KierunekDane> Kierunki);

/// <param name="DoPunktu">Kod punktu docelowego albo <c>null</c>, gdy kierunek prowadzi do sali.</param>
/// <param name="DoSali">Symbol sali docelowej; sala leży na piętrze <paramref name="PietroSali"/> tego samego budynku.</param>
/// <param name="AzymutPowrotu">Azymut kierunku powrotnego w punkcie docelowym albo <c>null</c>, gdy powrotu nie ma.</param>
public sealed record KierunekDane(int Azymut, string? DoPunktu, string? DoSali, int? PietroSali, decimal Waga, RodzajPrzejscia Rodzaj,
    string? OpisPrzejscia, string? NotatkaDojscia, bool CzyAktywny, bool CzyDostepnyBezSchodow, int? AzymutPowrotu);

public sealed record SalaDane(string Symbol, string Typ, string? PunktWejsciowy, string Nazwa, string? Aliasy, string? Opis,
    string? OpisGlosowy, int? LiczbaMiejsc, bool CzyDostepnaDlaWozkow, bool CzyAktywna, List<UdogodnienieDane> Udogodnienia, List<ZdjecieDane> Zdjecia);

public sealed record UdogodnienieDane(string Nazwa, string? Uwagi);

public sealed record ZdjecieDane(string SciezkaPliku, string TekstAlternatywny, string? OpisRozszerzony, string Zrodlo, string Licencja,
    int? Szerokosc, int? Wysokosc, int Kolejnosc, bool CzyDekoracyjne);

public sealed record PodsumowanieDanych(int Budynki, int Punkty, int Kierunki, int Sale, int Zdjecia);

/// <summary>
/// Przenoszenie danych budynków między bazą a plikiem w repozytorium. Baza leży poza katalogiem projektu,
/// więc git jej nie widzi; plik i katalog zdjęć — tak. Redaktor wprowadza dane w panelu, zapisuje je do pliku
/// i zatwierdza w repozytorium, a aplikacja uruchomiona na pustej bazie wczytuje je z pliku.
/// Do pliku NIE trafiają konta, zgłoszenia dostępności, rejestr zmian ani zapytania użytkowników.
/// </summary>
public sealed class DaneBudynkowService(IAdministracjaRepozytorium repo, IOptions<DaneBudynkowOptions> opcje, IWebHostEnvironment srodowisko)
{
    public const int WersjaFormatu = 1;

    private static readonly JsonSerializerOptions OpcjeJson = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        // Polskie znaki jako znaki, nie \uXXXX — plik ma się dać czytać i porównywać w gicie.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Serializuj(PlikDanych dane) => JsonSerializer.Serialize(dane, OpcjeJson) + "\n";

    public static PlikDanych? Deserializuj(string json) => JsonSerializer.Deserialize<PlikDanych>(json, OpcjeJson);

    public string SciezkaPliku => Path.GetFullPath(Path.Combine(srodowisko.ContentRootPath, opcje.Value.PlikDanych));

    /// <summary>Data ostatniego zapisu pliku albo <c>null</c>, gdy pliku nie ma.</summary>
    public DateTime? DataPliku => File.Exists(SciezkaPliku) ? File.GetLastWriteTime(SciezkaPliku) : null;

    // --- Zapis: baza → plik ---------------------------------------------------------------------------

    public async Task<PodsumowanieDanych> ZapiszDoPlikuAsync(CancellationToken ct = default)
    {
        var dane = await OdczytajZBazyAsync(ct);
        Directory.CreateDirectory(Path.GetDirectoryName(SciezkaPliku)!);
        await File.WriteAllTextAsync(SciezkaPliku, Serializuj(dane), ct);
        return Podsumuj(dane);
    }

    /// <summary>Cała zawartość bazy w postaci pliku. Kolejność jest stała (kody, numery, azymuty), żeby zmiany w gicie były czytelne.</summary>
    public async Task<PlikDanych> OdczytajZBazyAsync(CancellationToken ct = default)
    {
        var budynki = await repo.Zapytanie<Budynek>().AsNoTracking().AsSplitQuery()
            .Include(b => b.PunktWejsciaGlownego)
            .Include(b => b.Pietra).ThenInclude(p => p.PunktyRuchu).ThenInclude(p => p.TypPunktu)
            .Include(b => b.Pietra).ThenInclude(p => p.PunktyRuchu).ThenInclude(p => p.Zdjecia)
            .Include(b => b.Pietra).ThenInclude(p => p.PunktyRuchu).ThenInclude(p => p.PunktUdogodnienia).ThenInclude(u => u.Udogodnienie)
            .Include(b => b.Pietra).ThenInclude(p => p.Sale).ThenInclude(s => s.TypSali)
            .Include(b => b.Pietra).ThenInclude(p => p.Sale).ThenInclude(s => s.PunktWejsciowy)
            .Include(b => b.Pietra).ThenInclude(p => p.Sale).ThenInclude(s => s.Zdjecia)
            .Include(b => b.Pietra).ThenInclude(p => p.Sale).ThenInclude(s => s.SalaUdogodnienia).ThenInclude(u => u.Udogodnienie)
            .OrderBy(b => b.Kod)
            .ToListAsync(ct);

        var kierunki = (await repo.Zapytanie<Kierunek>().AsNoTracking()
                .Include(k => k.PunktDocelowy)
                .Include(k => k.SalaDocelowa!).ThenInclude(s => s.Pietro)
                .Include(k => k.KierunekPowrotny)
                .ToListAsync(ct))
            .ToLookup(k => k.PunktZrodlowyId);

        static List<ZdjecieDane> Zdjecia(IEnumerable<Zdjecie> zdjecia) => zdjecia
            .OrderBy(z => z.Kolejnosc).ThenBy(z => z.Id)
            .Select(z => new ZdjecieDane(z.SciezkaPliku, z.TekstAlternatywny, z.OpisRozszerzony, z.Zrodlo, z.Licencja,
                z.Szerokosc, z.Wysokosc, z.Kolejnosc, z.CzyDekoracyjne))
            .ToList();

        return new PlikDanych(
            WersjaFormatu,
            await repo.Zapytanie<TypSali>().AsNoTracking().OrderBy(t => t.Nazwa).Select(t => new SlownikDane(t.Nazwa, t.Opis, null)).ToListAsync(ct),
            await repo.Zapytanie<Udogodnienie>().AsNoTracking().OrderBy(u => u.Nazwa).Select(u => new SlownikDane(u.Nazwa, u.Opis, u.Ikona)).ToListAsync(ct),
            budynki.Select(b => new BudynekDane(b.Kod, b.Nazwa, b.Adres, b.Opis, b.OpisDostepnosciArchitektonicznej, b.CzyMaWinde, b.CzyAktywny,
                b.PunktWejsciaGlownego?.Kod,
                b.Pietra.OrderBy(p => p.Numer).Select(p => new PietroDane(p.Numer, p.Nazwa, p.Opis,
                    p.PunktyRuchu.OrderBy(pr => pr.Kod).Select(pr => new PunktDane(pr.Kod, pr.TypPunktu.Nazwa, pr.Nazwa, pr.Opis, pr.OpisGlosowy,
                        pr.AzymutDomyslny, pr.X, pr.Y, pr.CzyAktywny,
                        pr.PunktUdogodnienia.OrderBy(u => u.Udogodnienie.Nazwa).Select(u => new UdogodnienieDane(u.Udogodnienie.Nazwa, u.Uwagi)).ToList(),
                        Zdjecia(pr.Zdjecia),
                        kierunki[pr.Id].OrderBy(k => k.Azymut).Select(k => new KierunekDane(k.Azymut, k.PunktDocelowy?.Kod, k.SalaDocelowa?.Symbol,
                            k.SalaDocelowa?.Pietro.Numer, k.Waga, k.RodzajPrzejscia, k.OpisPrzejscia, k.NotatkaDojscia, k.CzyAktywny,
                            k.CzyDostepnyBezSchodow, k.KierunekPowrotny?.Azymut)).ToList())).ToList(),
                    p.Sale.OrderBy(s => s.Symbol).Select(s => new SalaDane(s.Symbol, s.TypSali.Nazwa, s.PunktWejsciowy?.Kod, s.Nazwa, s.Aliasy,
                        s.Opis, s.OpisGlosowy, s.LiczbaMiejsc, s.CzyDostepnaDlaWozkow, s.CzyAktywna,
                        s.SalaUdogodnienia.OrderBy(u => u.Udogodnienie.Nazwa).Select(u => new UdogodnienieDane(u.Udogodnienie.Nazwa, u.Uwagi)).ToList(),
                        Zdjecia(s.Zdjecia))).ToList())).ToList())).ToList());
    }

    public static PodsumowanieDanych Podsumuj(PlikDanych dane)
    {
        var pietra = dane.Budynki.SelectMany(b => b.Pietra).ToList();
        var punkty = pietra.SelectMany(p => p.Punkty).ToList();
        var sale = pietra.SelectMany(p => p.Sale).ToList();
        return new PodsumowanieDanych(dane.Budynki.Count, punkty.Count, punkty.Sum(p => p.Kierunki.Count), sale.Count,
            punkty.Sum(p => p.Zdjecia.Count) + sale.Sum(s => s.Zdjecia.Count));
    }

    // --- Odczyt: plik → pusta baza ------------------------------------------------------------------

    /// <summary>
    /// Wczytuje plik, jeśli w bazie nie ma jeszcze żadnego budynku. Istniejących danych nigdy nie nadpisuje.
    /// Zwraca podsumowanie albo <c>null</c>, gdy niczego nie wczytano (dane już są albo pliku nie ma).
    /// </summary>
    public async Task<PodsumowanieDanych?> WczytajDoPustejBazyAsync(CancellationToken ct = default)
    {
        if (!File.Exists(SciezkaPliku) || await repo.Zapytanie<Budynek>().AnyAsync(ct))
        {
            return null;
        }

        var dane = Deserializuj(await File.ReadAllTextAsync(SciezkaPliku, ct))
            ?? throw new InvalidDataException($"Plik danych „{SciezkaPliku}” jest pusty.");
        if (dane.Wersja != WersjaFormatu)
        {
            throw new InvalidDataException($"Plik danych ma wersję {dane.Wersja}, a aplikacja czyta wersję {WersjaFormatu}.");
        }

        await using var transakcja = await repo.RozpocznijTransakcjeAsync(ct);
        await WczytajAsync(dane, ct);
        await transakcja.CommitAsync(ct);
        return Podsumuj(dane);
    }

    private async Task WczytajAsync(PlikDanych dane, CancellationToken ct)
    {
        // Słowniki: pozycje z pliku, których w bazie nie ma, dochodzą; istniejące zostają bez zmian.
        var typySal = await repo.Zapytanie<TypSali>().ToDictionaryAsync(t => t.Nazwa, ct);
        foreach (var t in dane.TypySal.Where(t => !typySal.ContainsKey(t.Nazwa)))
        {
            repo.Dodaj(typySal[t.Nazwa] = new TypSali { Nazwa = t.Nazwa, Opis = t.Opis });
        }

        var udogodnienia = await repo.Zapytanie<Udogodnienie>().ToDictionaryAsync(u => u.Nazwa, ct);
        foreach (var u in dane.Udogodnienia.Where(u => !udogodnienia.ContainsKey(u.Nazwa)))
        {
            repo.Dodaj(udogodnienia[u.Nazwa] = new Udogodnienie { Nazwa = u.Nazwa, Opis = u.Opis, Ikona = u.Ikona });
        }

        // Typy punktów nie mają strony w panelu — pochodzą z migracji, więc plik może używać tylko istniejących.
        var typyPunktow = await repo.Zapytanie<TypPunktu>().ToDictionaryAsync(t => t.Nazwa, ct);

        static T Znajdz<T>(IReadOnlyDictionary<string, T> slownik, string klucz, string co) =>
            slownik.TryGetValue(klucz, out var wartosc)
                ? wartosc
                : throw new InvalidDataException($"Plik danych odwołuje się do nieistniejącej pozycji: {co} „{klucz}”.");

        static Zdjecie Zdjecie(ZdjecieDane z) => new()
        {
            SciezkaPliku = z.SciezkaPliku, TekstAlternatywny = z.TekstAlternatywny, OpisRozszerzony = z.OpisRozszerzony,
            Zrodlo = z.Zrodlo, Licencja = z.Licencja, Szerokosc = z.Szerokosc, Wysokosc = z.Wysokosc,
            Kolejnosc = z.Kolejnosc, CzyDekoracyjne = z.CzyDekoracyjne,
        };

        var punkty = new Dictionary<string, PunktRuchu>();                                // kod punktu jest unikalny w całej bazie
        var sale = new Dictionary<(string Budynek, int Pietro, string Symbol), Sala>();
        var kierunki = new Dictionary<(string Punkt, int Azymut), Kierunek>();
        var wejscia = new List<(Budynek Budynek, string KodPunktu)>();
        var powroty = new List<(Kierunek Kierunek, string KodCelu, int AzymutPowrotu)>();

        // Krok 1a: budynki, piętra, punkty.
        foreach (var b in dane.Budynki)
        {
            var budynek = new Budynek
            {
                Kod = b.Kod, Nazwa = b.Nazwa, Adres = b.Adres, Opis = b.Opis,
                OpisDostepnosciArchitektonicznej = b.OpisDostepnosciArchitektonicznej, CzyMaWinde = b.CzyMaWinde, CzyAktywny = b.CzyAktywny,
            };
            repo.Dodaj(budynek);
            if (b.PunktWejsciaGlownego is not null)
            {
                wejscia.Add((budynek, b.PunktWejsciaGlownego));
            }

            foreach (var p in b.Pietra)
            {
                var pietro = new Pietro { Budynek = budynek, Numer = p.Numer, Nazwa = p.Nazwa, Opis = p.Opis };
                foreach (var pr in p.Punkty)
                {
                    punkty.Add(pr.Kod, new PunktRuchu
                    {
                        Pietro = pietro, TypPunktu = Znajdz(typyPunktow, pr.Typ, "typ punktu"), Kod = pr.Kod, Nazwa = pr.Nazwa,
                        Opis = pr.Opis, OpisGlosowy = pr.OpisGlosowy, AzymutDomyslny = pr.AzymutDomyslny, X = pr.X, Y = pr.Y,
                        CzyAktywny = pr.CzyAktywny,
                        Zdjecia = pr.Zdjecia.Select(Zdjecie).ToList(),
                        PunktUdogodnienia = pr.Udogodnienia
                            .Select(u => new PunktUdogodnienie { Udogodnienie = Znajdz(udogodnienia, u.Nazwa, "udogodnienie"), Uwagi = u.Uwagi })
                            .ToList(),
                    });
                }

                // Krok 1b: sale — punkt wejściowy może leżeć na innym piętrze, więc szukamy go dopiero niżej.
                foreach (var s in p.Sale)
                {
                    sale.Add((b.Kod, p.Numer, s.Symbol), new Sala
                    {
                        Pietro = pietro, TypSali = Znajdz(typySal, s.Typ, "typ sali"), Symbol = s.Symbol, Nazwa = s.Nazwa, Aliasy = s.Aliasy,
                        Opis = s.Opis, OpisGlosowy = s.OpisGlosowy, LiczbaMiejsc = s.LiczbaMiejsc,
                        CzyDostepnaDlaWozkow = s.CzyDostepnaDlaWozkow, CzyAktywna = s.CzyAktywna,
                        Zdjecia = s.Zdjecia.Select(Zdjecie).ToList(),
                        SalaUdogodnienia = s.Udogodnienia
                            .Select(u => new SalaUdogodnienie { Udogodnienie = Znajdz(udogodnienia, u.Nazwa, "udogodnienie"), Uwagi = u.Uwagi })
                            .ToList(),
                    });
                }
            }
        }

        // Krok 1c: wskazania między rekordami — punkty wejściowe sal i kierunki.
        foreach (var b in dane.Budynki)
        {
            foreach (var p in b.Pietra)
            {
                foreach (var s in p.Sale.Where(s => s.PunktWejsciowy is not null))
                {
                    sale[(b.Kod, p.Numer, s.Symbol)].PunktWejsciowy = Znajdz(punkty, s.PunktWejsciowy!, "punkt wejściowy sali");
                }

                foreach (var pr in p.Punkty)
                {
                    foreach (var k in pr.Kierunki)
                    {
                        var kierunek = new Kierunek
                        {
                            PunktZrodlowy = punkty[pr.Kod],
                            PunktDocelowy = k.DoPunktu is null ? null : Znajdz(punkty, k.DoPunktu, "punkt docelowy kierunku"),
                            SalaDocelowa = k.DoSali is null
                                ? null
                                : sale.TryGetValue((b.Kod, k.PietroSali ?? p.Numer, k.DoSali), out var sala)
                                    ? sala
                                    : throw new InvalidDataException($"Plik danych odwołuje się do nieistniejącej pozycji: sala „{k.DoSali}”."),
                            Azymut = k.Azymut, Waga = k.Waga, RodzajPrzejscia = k.Rodzaj, OpisPrzejscia = k.OpisPrzejscia,
                            NotatkaDojscia = k.NotatkaDojscia, CzyAktywny = k.CzyAktywny, CzyDostepnyBezSchodow = k.CzyDostepnyBezSchodow,
                        };
                        kierunki.Add((pr.Kod, k.Azymut), kierunek);
                        if (k is { DoPunktu: not null, AzymutPowrotu: int azymutPowrotu })
                        {
                            powroty.Add((kierunek, k.DoPunktu, azymutPowrotu));
                        }
                    }
                }
            }
        }

        foreach (var punkt in punkty.Values) repo.Dodaj(punkt);
        foreach (var sala in sale.Values) repo.Dodaj(sala);
        foreach (var kierunek in kierunki.Values) repo.Dodaj(kierunek);
        await repo.ZapiszAsync(ct); // wszystkie rekordy dostają identyfikatory

        // Krok 2: pary, które wskazują na siebie nawzajem (budynek ↔ wejście główne, kierunek ↔ powrót),
        // nie dają się wstawić jednym INSERT-em — ustawiamy je, gdy obie strony już istnieją.
        foreach (var (budynek, kodPunktu) in wejscia)
        {
            budynek.PunktWejsciaGlownego = Znajdz(punkty, kodPunktu, "punkt wejścia głównego");
        }

        foreach (var (kierunek, kodCelu, azymutPowrotu) in powroty)
        {
            kierunek.KierunekPowrotny = kierunki.TryGetValue((kodCelu, azymutPowrotu), out var powrot)
                ? powrot
                : throw new InvalidDataException(
                    $"Plik danych odwołuje się do nieistniejącej pozycji: kierunek powrotny {azymutPowrotu}° w punkcie „{kodCelu}”.");
        }

        await repo.ZapiszAsync(ct);
    }
}
