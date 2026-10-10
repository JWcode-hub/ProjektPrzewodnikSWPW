using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.Web.ViewModels;

public sealed record BudynkiViewModel(IReadOnlyList<BudynekNaLiscie> Budynki);

public sealed record LinkDoMiejsca(string Nazwa, string Href);

public sealed record PietroViewModel(string Nazwa, IReadOnlyList<LinkDoMiejsca> Punkty);

public sealed record BudynekViewModel(
    string Kod,
    string Nazwa,
    string? Adres,
    string? OpisDostepnosciArchitektonicznej,
    LinkDoMiejsca? Start,
    IReadOnlyList<PietroViewModel> Pietra);

/// <summary>
/// Pozycja listy przejść. <see cref="Href"/> = <c>null</c> oznacza brak przejścia — widok pokazuje wtedy
/// zwykły tekst, nie link i nie wyłączony przycisk. Dwa widoczne wiersze: <see cref="Nazwa"/> (czynność
/// i cel) oraz <see cref="Szczegol"/> (rodzaj przejścia, odległość, notatka o dojściu, opis).
/// </summary>
public sealed record KierunekNaEkranie(string Parametr, string Nazwa, string? Szczegol, string? Href);

public sealed record ZdjecieNaEkranie(string Src, string Alt, int? Szerokosc, int? Wysokosc, string? Podpis)
{
    /// <summary>Zdjęcie dekoracyjne ma pusty tekst alternatywny i nie ma podpisu.</summary>
    public static ZdjecieNaEkranie Z(ZdjecieMiejsca z, string domyslnyPodpis) => new(
        z.SciezkaPliku.StartsWith('/') ? z.SciezkaPliku : $"/media/{z.SciezkaPliku}",
        z.CzyDekoracyjne ? "" : z.TekstAlternatywny,
        z.Szerokosc,
        z.Wysokosc,
        z.CzyDekoracyjne ? null : z.OpisRozszerzony ?? domyslnyPodpis);
}

/// <param name="TekstDoPrzeczytania">Opis głosowy miejsca i notatki o dojściu — dla mowy syntetycznej i skrótu Alt+P.</param>
/// <param name="Komunikat">Opis przeszkody po nieudanej próbie przejścia albo <c>null</c>.</param>
public sealed record MiejsceViewModel(
    string Nazwa,
    string? Opis,
    string? TekstDoPrzeczytania,
    string NazwaBudynku,
    string NazwaPietra,
    string KodBudynku,
    string AdresBudynku,
    IReadOnlyList<ZdjecieNaEkranie> Zdjecia,
    IReadOnlyList<KierunekNaEkranie> Kierunki,
    string? Komunikat);

public sealed record SalaViewModel(
    int Id,
    string Symbol,
    string Nazwa,
    string TypSali,
    string? Opis,
    string? TekstDoPrzeczytania,
    string NazwaBudynku,
    string NazwaPietra,
    int? LiczbaMiejsc,
    bool CzyDostepnaDlaWozkow,
    IReadOnlyList<ZdjecieNaEkranie> Zdjecia,
    IReadOnlyList<string> Udogodnienia,
    LinkDoMiejsca? Powrot);
