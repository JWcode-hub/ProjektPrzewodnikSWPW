namespace PrzewodnikSWPW.Web.Services;

// Modele, które serwis nawigacji oddaje kontrolerom. To nie są encje bazy danych.

/// <summary>Miejsce widziane przez użytkownika stojącego w punkcie ruchu ze zwrotem <see cref="Zwrot"/>.</summary>
/// <param name="Kierunki">Zawsze cztery pozycje w kolejności: prosto, w lewo, w prawo, do tyłu.</param>
public sealed record WidokMiejsca(
    int PunktId,
    string Kod,
    string Nazwa,
    string? Opis,
    string? OpisGlosowy,
    int Zwrot,
    string KodBudynku,
    string NazwaBudynku,
    int NumerPietra,
    string NazwaPietra,
    IReadOnlyList<ZdjecieMiejsca> Zdjecia,
    IReadOnlyList<OpcjaKierunku> Kierunki);

public sealed record ZdjecieMiejsca(
    string SciezkaPliku,
    string TekstAlternatywny,
    bool CzyDekoracyjne,
    string? OpisRozszerzony,
    int? Szerokosc,
    int? Wysokosc);

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
/// do sali <see cref="PunktId"/> to punkt przed jej drzwiami.
/// </summary>
public sealed record WynikPrzejscia(
    RodzajWynikuPrzejscia Rodzaj,
    int PunktId,
    int? SalaId,
    int Zwrot,
    string Opis)
{
    public bool CzyPrzeszedl => Rodzaj != RodzajWynikuPrzejscia.BrakPrzejscia;
}
