using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.Services;

// Modele, które serwis nawigacji oddaje kontrolerom. To nie są encje bazy danych.

/// <summary>
/// Położenie punktu w adresie /spacer/{KodBudynku}/{NumerPietra}/{SegmentPunktu}. Cały stan spaceru
/// (punkt i zwrot) jest w adresie strony, nie w sesji — działają przycisk Wstecz, zakładki i wysłanie linku.
/// </summary>
public sealed record AdresMiejsca(string KodBudynku, int NumerPietra, string SegmentPunktu)
{
    public static AdresMiejsca Z(PunktRuchu punkt) => Z(punkt.Pietro.Budynek.Kod, punkt.Pietro.Numer, punkt.Kod);

    /// <summary>Kod „A-0-P04” w budynku A na piętrze 0 daje segment „P04”; inny kod zostaje w całości.</summary>
    public static AdresMiejsca Z(string kodBudynku, int numerPietra, string kodPunktu)
    {
        var prefiks = $"{kodBudynku}-{numerPietra}-";
        var segment = kodPunktu.StartsWith(prefiks, StringComparison.OrdinalIgnoreCase) && kodPunktu.Length > prefiks.Length
            ? kodPunktu[prefiks.Length..]
            : kodPunktu;
        return new AdresMiejsca(kodBudynku, numerPietra, segment);
    }
}

/// <summary>Miejsce widziane przez użytkownika stojącego w punkcie ruchu ze zwrotem <see cref="Zwrot"/>.</summary>
/// <param name="Kierunki">Zawsze cztery pozycje w kolejności: prosto, w lewo, w prawo, do tyłu.</param>
public sealed record WidokMiejsca(
    int PunktId,
    string Kod,
    string Nazwa,
    string? Opis,
    string? OpisGlosowy,
    int Zwrot,
    AdresMiejsca Adres,
    string NazwaBudynku,
    string NazwaPietra,
    IReadOnlyList<ZdjecieMiejsca> Zdjecia,
    IReadOnlyList<OpcjaKierunku> Kierunki);

public sealed record ZdjecieMiejsca(
    string SciezkaPliku,
    string TekstAlternatywny,
    bool CzyDekoracyjne,
    string? OpisRozszerzony,
    int? Szerokosc,
    int? Wysokosc)
{
    public static ZdjecieMiejsca Z(Zdjecie z) =>
        new(z.SciezkaPliku, z.TekstAlternatywny, z.CzyDekoracyjne, z.OpisRozszerzony, z.Szerokosc, z.Wysokosc);
}

/// <summary>
/// Jedna z czterech pozycji listy kierunków. Gdy <see cref="CzyMozliwy"/> = false, pozycja jest
/// zwykłym tekstem z opisem przeszkody, nie linkiem — to punkt orientacyjny.
/// </summary>
/// <param name="Nazwa">Pierwszy wiersz: czynność i cel („Idź prosto — hol główny przy portierni”) albo „W lewo — brak przejścia.”</param>
/// <param name="Szczegol">Drugi wiersz: rodzaj przejścia, odległość, notatka o dojściu i opis przejścia; dla braku przejścia — opis przeszkody albo <c>null</c>.</param>
/// <param name="Tekst">Całość w jednym zdaniu („Idź prosto — hol główny przy portierni, 6 metrów”) — do ogłoszeń.</param>
/// <param name="Notatka">Notatka o dojściu, gdy skręt nie jest ćwierć obrotem — osobno, dla mowy syntetycznej i kroków trasy.</param>
public sealed record OpcjaKierunku(
    KierunekWzgledny Kierunek,
    bool CzyMozliwy,
    string Nazwa,
    string? Szczegol,
    string Tekst,
    string? Notatka = null,
    int? PunktDocelowyId = null,
    int? SalaDocelowaId = null,
    decimal? OdlegloscMetry = null,
    int? KierunekId = null);

public enum RodzajWynikuPrzejscia
{
    DoPunktu,
    DoSali,
    BrakPrzejscia,
}

/// <summary>
/// Wynik próby przejścia. <see cref="PunktId"/> i <see cref="Zwrot"/> mówią, gdzie użytkownik jest
/// i w którą stronę patrzy PO próbie: po przejściu zwrot = azymut przebytej krawędzi; przy braku
/// przejścia punkt i zwrot się nie zmieniają, a <see cref="Opis"/> mówi dlaczego. Przy wejściu
/// do sali <see cref="PunktId"/> to punkt przed jej drzwiami. <see cref="Adres"/> to adres tego punktu.
/// </summary>
public sealed record WynikPrzejscia(
    RodzajWynikuPrzejscia Rodzaj,
    int PunktId,
    int? SalaId,
    int Zwrot,
    string Opis,
    AdresMiejsca Adres)
{
    public bool CzyPrzeszedl => Rodzaj != RodzajWynikuPrzejscia.BrakPrzejscia;
}

public sealed record BudynekNaLiscie(string Kod, string Nazwa, string? Adres);

public sealed record DostepnoscBudynku(string Nazwa, string? Adres, string? OpisDostepnosciArchitektonicznej);

public sealed record PunktNaLiscie(AdresMiejsca Adres, string Nazwa, int AzymutDomyslny);

public sealed record PietroNaLiscie(int Numer, string Nazwa, IReadOnlyList<PunktNaLiscie> Punkty);

/// <param name="PunktStartowy">Wejście główne budynku albo <c>null</c>, gdy go nie ustawiono.</param>
public sealed record WidokBudynku(
    string Kod,
    string Nazwa,
    string? Adres,
    string? OpisDostepnosciArchitektonicznej,
    IReadOnlyList<PietroNaLiscie> Pietra,
    PunktNaLiscie? PunktStartowy);

public sealed record UdogodnienieSali(string Nazwa, string? Uwagi);

/// <summary>Karta sali. <see cref="Powrot"/> prowadzi na korytarz przed drzwiami, twarzą od drzwi.</summary>
public sealed record WidokSali(
    int Id,
    string Symbol,
    string Nazwa,
    string TypSali,
    string? Opis,
    string? OpisGlosowy,
    string NazwaBudynku,
    string NazwaPietra,
    int? LiczbaMiejsc,
    bool CzyDostepnaDlaWozkow,
    IReadOnlyList<ZdjecieMiejsca> Zdjecia,
    IReadOnlyList<UdogodnienieSali> Udogodnienia,
    PunktNaLiscie? Powrot,
    int? ZwrotPowrotu);
