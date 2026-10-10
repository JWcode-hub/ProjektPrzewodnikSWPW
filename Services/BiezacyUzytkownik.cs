using System.Security.Claims;

namespace PrzewodnikSWPW.Web.Services;

public interface IBiezacyUzytkownik
{
    /// <summary>Identyfikator zalogowanego użytkownika (AspNetUsers.Id) albo <c>null</c> — wczytanie danych z pliku, zadanie w tle, gość.</summary>
    string? Id { get; }
}

public sealed class BiezacyUzytkownik(IHttpContextAccessor dostep) : IBiezacyUzytkownik
{
    public string? Id => dostep.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
}
