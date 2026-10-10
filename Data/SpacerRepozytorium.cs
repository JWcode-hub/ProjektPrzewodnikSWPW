using Microsoft.EntityFrameworkCore;
using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.Data;

/// <summary>Odczyt danych do spaceru po budynku. Wszystko bez śledzenia zmian — spacer niczego nie zapisuje.</summary>
public interface ISpacerRepozytorium
{
    /// <summary>
    /// Punkt ruchu z piętrem, budynkiem, zdjęciami i kierunkami wychodzącymi (z celami
    /// i utrudnieniami) albo <c>null</c>.
    /// </summary>
    Task<PunktRuchu?> PobierzPunktZKierunkamiAsync(int punktId, CancellationToken ct = default);

    /// <summary>Aktywne budynki, posortowane po kodzie.</summary>
    Task<List<Budynek>> PobierzBudynkiAsync(CancellationToken ct = default);

    /// <summary>Aktywny budynek z piętrami, ich aktywnymi punktami ruchu i wejściem głównym albo <c>null</c>.</summary>
    Task<Budynek?> PobierzBudynekAsync(string kodBudynku, CancellationToken ct = default);

    /// <summary>
    /// Identyfikator punktu z adresu /spacer/{budynek}/{pietro}/{punkt}. Segment punktu to pełny kod
    /// („A-0-P04”) albo jego część po prefiksie budynku i piętra („P04”).
    /// </summary>
    Task<int?> ZnajdzPunktIdAsync(string kodBudynku, int numerPietra, string segmentPunktu, CancellationToken ct = default);

    /// <summary>Punkt ruchu z piętrem i budynkiem (do zbudowania adresu) albo <c>null</c>.</summary>
    Task<PunktRuchu?> PobierzPunktAsync(int punktId, CancellationToken ct = default);

    /// <summary>Sala ze zdjęciami, udogodnieniami, piętrem, budynkiem i punktem wejściowym albo <c>null</c>.</summary>
    Task<Sala?> PobierzSaleAsync(int salaId, CancellationToken ct = default);
}

public sealed class SpacerRepozytorium(PrzewodnikDbContext db) : ISpacerRepozytorium
{
    public Task<PunktRuchu?> PobierzPunktZKierunkamiAsync(int punktId, CancellationToken ct = default) =>
        db.PunktyRuchu
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.Pietro).ThenInclude(p => p.Budynek)
            .Include(p => p.Zdjecia.OrderBy(z => z.Kolejnosc))
            .Include(p => p.KierunkiWychodzace).ThenInclude(k => k.Utrudnienia)
            .Include(p => p.KierunkiWychodzace).ThenInclude(k => k.PunktDocelowy!).ThenInclude(p => p.Utrudnienia)
            .Include(p => p.KierunkiWychodzace).ThenInclude(k => k.PunktDocelowy!).ThenInclude(p => p.Pietro).ThenInclude(p => p.Budynek)
            .Include(p => p.KierunkiWychodzace).ThenInclude(k => k.SalaDocelowa)
            .SingleOrDefaultAsync(p => p.Id == punktId, ct);

    public Task<List<Budynek>> PobierzBudynkiAsync(CancellationToken ct = default) =>
        db.Budynki
            .AsNoTracking()
            .Where(b => b.CzyAktywny)
            .OrderBy(b => b.Kod)
            .ToListAsync(ct);

    public Task<Budynek?> PobierzBudynekAsync(string kodBudynku, CancellationToken ct = default) =>
        db.Budynki
            .AsNoTracking()
            .AsSplitQuery()
            .Include(b => b.Pietra.OrderBy(p => p.Numer))
                .ThenInclude(p => p.PunktyRuchu.Where(pr => pr.CzyAktywny).OrderBy(pr => pr.Kod))
            .Include(b => b.PunktWejsciaGlownego!).ThenInclude(p => p.Pietro)
            .SingleOrDefaultAsync(b => b.Kod == kodBudynku && b.CzyAktywny, ct);

    public async Task<int?> ZnajdzPunktIdAsync(string kodBudynku, int numerPietra, string segmentPunktu, CancellationToken ct = default)
    {
        var pelnyKod = $"{kodBudynku}-{numerPietra}-{segmentPunktu}";
        return await db.PunktyRuchu
            .Where(p => p.Pietro.Budynek.Kod == kodBudynku
                        && p.Pietro.Numer == numerPietra
                        && (p.Kod == segmentPunktu || p.Kod == pelnyKod))
            .Select(p => (int?)p.Id)
            .SingleOrDefaultAsync(ct);
    }

    public Task<PunktRuchu?> PobierzPunktAsync(int punktId, CancellationToken ct = default) =>
        db.PunktyRuchu
            .AsNoTracking()
            .Include(p => p.Pietro).ThenInclude(p => p.Budynek)
            .SingleOrDefaultAsync(p => p.Id == punktId, ct);

    public Task<Sala?> PobierzSaleAsync(int salaId, CancellationToken ct = default) =>
        db.Sale
            .AsNoTracking()
            .AsSplitQuery()
            .Include(s => s.Pietro).ThenInclude(p => p.Budynek)
            .Include(s => s.TypSali)
            .Include(s => s.Zdjecia.OrderBy(z => z.Kolejnosc))
            .Include(s => s.SalaUdogodnienia).ThenInclude(su => su.Udogodnienie)
            .Include(s => s.PunktWejsciowy!).ThenInclude(p => p.Pietro).ThenInclude(p => p.Budynek)
            .SingleOrDefaultAsync(s => s.Id == salaId, ct);
}
