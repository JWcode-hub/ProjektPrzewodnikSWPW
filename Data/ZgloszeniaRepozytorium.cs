using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.Data;

public interface IZgloszeniaRepozytorium
{
    Task DodajAsync(ZgloszenieDostepnosci zgloszenie, CancellationToken ct = default);
}

public sealed class ZgloszeniaRepozytorium(PrzewodnikDbContext db) : IZgloszeniaRepozytorium
{
    public async Task DodajAsync(ZgloszenieDostepnosci zgloszenie, CancellationToken ct = default)
    {
        db.ZgloszeniaDostepnosci.Add(zgloszenie);
        await db.SaveChangesAsync(ct);
    }
}
