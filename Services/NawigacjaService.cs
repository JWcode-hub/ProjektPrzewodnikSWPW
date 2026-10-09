using System.Globalization;
using PrzewodnikSWPW.Web.Data;
using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.Services;

/// <summary>
/// Spacer po grafie budynku: widok miejsca z czterema kierunkami i przejście jedną krawędzią.
/// Wszystkie przeliczenia azymutów idą przez <see cref="Azymuty"/>.
/// </summary>
public sealed class NawigacjaService(ISpacerRepozytorium repozytorium, TimeProvider czas)
{
    private static readonly CultureInfo Polski = CultureInfo.GetCultureInfo("pl-PL");

    /// <summary>
    /// Widok punktu dla użytkownika zwróconego w stronę <paramref name="zwrot"/>. Lista kierunków ma
    /// ZAWSZE cztery pozycje w stałej kolejności — także gdy kierunku nie ma, jest nieaktywny albo
    /// zablokowany utrudnieniem. Zwraca <c>null</c>, gdy punkt nie istnieje albo jest nieaktywny.
    /// </summary>
    public async Task<WidokMiejsca?> PobierzMiejsce(int punktId, int zwrot, CancellationToken ct = default)
    {
        zwrot = Azymuty.Normalizuj(zwrot);
        var punkt = await repozytorium.PobierzPunktZKierunkamiAsync(punktId, ct);
        if (punkt is null || !punkt.CzyAktywny)
        {
            return null;
        }

        var teraz = czas.GetLocalNow().DateTime;
        var kierunki = Azymuty.KolejnoscNaLiscie
            .Select(wzgledny => ZbudujOpcje(punkt, wzgledny, zwrot, teraz))
            .ToList();

        var zdjecia = punkt.Zdjecia
            .OrderBy(z => z.Kolejnosc)
            .Select(z => new ZdjecieMiejsca(z.SciezkaPliku, z.TekstAlternatywny, z.CzyDekoracyjne,
                z.OpisRozszerzony, z.Szerokosc, z.Wysokosc))
            .ToList();

        return new WidokMiejsca(punkt.Id, punkt.Kod, punkt.Nazwa, punkt.Opis, punkt.OpisGlosowy, zwrot,
            punkt.Pietro.Budynek.Kod, punkt.Pietro.Budynek.Nazwa, punkt.Pietro.Numer, punkt.Pietro.Nazwa,
            zdjecia, kierunki);
    }

    /// <summary>
    /// Próba przejścia w kierunku względnym. Po przejściu NOWY ZWROT = azymut przebytej krawędzi.
    /// Przy braku przejścia użytkownik zostaje w miejscu z tym samym zwrotem.
    /// Zwraca <c>null</c>, gdy punkt startowy nie istnieje albo jest nieaktywny.
    /// </summary>
    public async Task<WynikPrzejscia?> Przejdz(int punktId, KierunekWzgledny kierunek, int zwrot, CancellationToken ct = default)
    {
        zwrot = Azymuty.Normalizuj(zwrot);
        var punkt = await repozytorium.PobierzPunktZKierunkamiAsync(punktId, ct);
        if (punkt is null || !punkt.CzyAktywny)
        {
            return null;
        }

        var opcja = ZbudujOpcje(punkt, kierunek, zwrot, czas.GetLocalNow().DateTime);
        if (!opcja.CzyMozliwy)
        {
            return new WynikPrzejscia(RodzajWynikuPrzejscia.BrakPrzejscia, punkt.Id, null, zwrot, opcja.Tekst);
        }

        var nowyZwrot = Azymuty.NaBezwzgledny(kierunek, zwrot);
        return opcja.PunktDocelowyId is int docelowy
            ? new WynikPrzejscia(RodzajWynikuPrzejscia.DoPunktu, docelowy, null, nowyZwrot, opcja.Tekst)
            : new WynikPrzejscia(RodzajWynikuPrzejscia.DoSali, punkt.Id, opcja.SalaDocelowaId, nowyZwrot, opcja.Tekst);
    }

    private static OpcjaKierunku ZbudujOpcje(PunktRuchu punkt, KierunekWzgledny wzgledny, int zwrot, DateTime teraz)
    {
        var azymut = Azymuty.NaBezwzgledny(wzgledny, zwrot);
        var krawedz = punkt.KierunkiWychodzace.SingleOrDefault(k => k.Azymut == azymut);
        var brak = $"{Azymuty.NazwaStrony(wzgledny)} — brak przejścia.";

        if (krawedz is null)
        {
            return new OpcjaKierunku(wzgledny, false, brak, null, brak);
        }

        var przeszkoda = Przeszkoda(krawedz, teraz);
        if (przeszkoda is not null)
        {
            return przeszkoda.Length == 0
                ? new OpcjaKierunku(wzgledny, false, brak, null, brak, KierunekId: krawedz.Id)
                : new OpcjaKierunku(wzgledny, false, brak, przeszkoda, $"{brak} {przeszkoda}", KierunekId: krawedz.Id);
        }

        var odleglosc = Metry(krawedz.Waga);
        var nazwa = $"{Azymuty.Etykieta(wzgledny)} — {NazwaCelu(krawedz)}";
        var notatka = Przytnij(krawedz.NotatkaDojscia);

        // Drugi wiersz: rodzaj i odległość, potem notatka o dojściu (jeśli jest), na końcu opis przejścia.
        var czesci = new[] { $"{krawedz.RodzajPrzejscia}, {odleglosc}.", notatka, Przytnij(krawedz.OpisPrzejscia) };
        var szczegol = string.Join(" ", czesci.Where(c => c is not null));

        return new OpcjaKierunku(wzgledny, true, nazwa, szczegol, $"{nazwa}, {odleglosc}", notatka,
            krawedz.PunktDocelowyId, krawedz.SalaDocelowaId, krawedz.Waga, krawedz.Id);
    }

    /// <summary>
    /// Opis przeszkody, gdy krawędzią nie da się teraz przejść; <c>null</c>, gdy przejście jest wolne.
    /// Dla kierunku nieaktywnego to jego opis orientacyjny (pusty tekst, gdy go nie wpisano).
    /// </summary>
    private static string? Przeszkoda(Kierunek krawedz, DateTime teraz)
    {
        if (!krawedz.CzyAktywny)
        {
            return Przytnij(krawedz.OpisPrzejscia) ?? string.Empty;
        }

        var utrudnienie = Obowiazujace(krawedz.Utrudnienia, teraz)
            ?? (krawedz.PunktDocelowy is null ? null : Obowiazujace(krawedz.PunktDocelowy.Utrudnienia, teraz));
        if (utrudnienie is not null)
        {
            var doKiedy = utrudnienie.ObowiazujeDo is DateTime koniec
                ? $" Przejście będzie otwarte po {koniec.ToString("d MMMM yyyy", Polski)}."
                : string.Empty;
            return $"Przejście czasowo zamknięte: {utrudnienie.Przyczyna.TrimEnd('.')}.{doKiedy}";
        }

        if (krawedz.PunktDocelowy is { CzyAktywny: false })
        {
            return "Dalsza część korytarza jest niedostępna.";
        }

        if (krawedz.SalaDocelowa is { CzyAktywna: false })
        {
            return "Sala jest niedostępna.";
        }

        return null;
    }

    private static Utrudnienie? Obowiazujace(IEnumerable<Utrudnienie> utrudnienia, DateTime teraz) =>
        utrudnienia.FirstOrDefault(u => u.ObowiazujeOd <= teraz && (u.ObowiazujeDo is null || teraz <= u.ObowiazujeDo));

    /// <summary>„korytarz zachodni przy sali A12” albo „drzwi do sali A12, pracownia komputerowa”.</summary>
    private static string NazwaCelu(Kierunek krawedz) => krawedz switch
    {
        { PunktDocelowy: { } punkt } => MalaLitera(punkt.Nazwa),
        { SalaDocelowa: { } sala } => $"drzwi do sali {sala.Symbol}, {MalaLitera(sala.Nazwa)}",
        _ => "dalej",
    };

    private static string MalaLitera(string tekst) =>
        tekst.Length == 0 ? tekst : char.ToLower(tekst[0], Polski) + tekst[1..];

    private static string? Przytnij(string? tekst) => string.IsNullOrWhiteSpace(tekst) ? null : tekst.Trim();

    /// <summary>Odległość z polską odmianą: 1 metr, 2 metry, 5 metrów, 12 metrów, 22 metry, 0,5 metra.</summary>
    public static string Metry(decimal metry)
    {
        if (metry != decimal.Truncate(metry))
        {
            return $"{metry.ToString("0.#", Polski)} metra";
        }

        var n = (long)metry;
        var forma = n == 1 ? "metr"
            : n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14 ? "metry"
            : "metrów";
        return $"{n} {forma}";
    }
}
