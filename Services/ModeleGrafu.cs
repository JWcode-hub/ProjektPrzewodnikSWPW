using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.Services;

/// <summary>Czasowa blokada (utrudnienie) z datami obowiązywania.</summary>
public sealed record OkresUtrudnienia(DateTime Od, DateTime? Do, string Przyczyna)
{
    public bool Obowiazuje(DateTime data) => Od <= data && (Do is null || data <= Do);
}

public sealed record WezelGrafu(
    int Id,
    string Nazwa,
    int AzymutDomyslny,
    bool CzyAktywny,
    IReadOnlyList<OkresUtrudnienia> Utrudnienia);

/// <summary>Krawędź grafu w pamięci — kopia wiersza Kierunek bez śledzenia EF.</summary>
public sealed record KrawedzGrafu(
    int Id,
    int Z,
    int? DoPunktu,
    int? DoSali,
    int Azymut,
    decimal Waga,
    RodzajPrzejscia Rodzaj,
    bool CzyAktywny,
    bool CzyDostepnyBezSchodow,
    string? OpisPrzejscia,
    string? NotatkaDojscia,
    IReadOnlyList<OkresUtrudnienia> Utrudnienia)
{
    /// <summary>
    /// Czy odcinek wymaga pokonania schodów. O schodach, windzie i podjeździe przesądza rodzaj przejścia:
    /// schody — zawsze, winda i podjazd — nigdy, bo same służą do ominięcia schodów. Pole
    /// „dostępne bez schodów” decyduje tylko dla korytarza i drzwi (np. korytarz ze stopniami).
    /// </summary>
    public bool CzySchody => Rodzaj switch
    {
        RodzajPrzejscia.Schody => true,
        RodzajPrzejscia.Winda or RodzajPrzejscia.Podjazd => false,
        _ => !CzyDostepnyBezSchodow,
    };
}

public sealed record SalaWGrafie(int Id, string Symbol, string Nazwa);

/// <summary>
/// Graf jednego budynku jako lista sąsiedztwa. Kilkaset wierzchołków — mieści się w pamięci,
/// więc algorytm trasy nie odpytuje bazy w pętli.
/// </summary>
public sealed class GrafBudynku
{
    private static readonly IReadOnlyList<KrawedzGrafu> Brak = [];

    public GrafBudynku(int budynekId, IEnumerable<WezelGrafu> wezly, IEnumerable<KrawedzGrafu> krawedzie, IEnumerable<SalaWGrafie> sale)
    {
        BudynekId = budynekId;
        Wezly = wezly.ToDictionary(w => w.Id);
        Sale = sale.ToDictionary(s => s.Id);
        Wychodzace = krawedzie
            .GroupBy(k => k.Z)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<KrawedzGrafu>)g.ToList());
    }

    public int BudynekId { get; }
    public IReadOnlyDictionary<int, WezelGrafu> Wezly { get; }
    public IReadOnlyDictionary<int, SalaWGrafie> Sale { get; }
    public IReadOnlyDictionary<int, IReadOnlyList<KrawedzGrafu>> Wychodzace { get; }

    public IReadOnlyList<KrawedzGrafu> Sasiedzi(int punktId) =>
        Wychodzace.TryGetValue(punktId, out var k) ? k : Brak;

    /// <summary>Krawędź „drzwi” z punktu wejściowego do sali (azymut = kierunek, w którym stoją drzwi).</summary>
    public KrawedzGrafu? KrawedzDoSali(int punktId, int salaId) =>
        Sasiedzi(punktId).FirstOrDefault(k => k.DoSali == salaId);
}
