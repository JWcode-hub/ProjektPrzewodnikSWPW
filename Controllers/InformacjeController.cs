using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PrzewodnikSWPW.Web.Services;
using PrzewodnikSWPW.Web.ViewModels;

namespace PrzewodnikSWPW.Web.Controllers;

/// <summary>
/// Strony wymagane przepisami, podlinkowane w stopce każdej strony: Deklaracja Dostępności,
/// wykaz skrótów, zgłoszenie braku dostępności, polityka prywatności.
/// </summary>
public class InformacjeController(
    NawigacjaService nawigacja,
    ZgloszeniaService zgloszenia,
    IOptions<DeklaracjaDostepnosciOptions> deklaracja,
    IOptions<KontaktAlternatywnyOptions> kontakt) : Controller
{
    [HttpGet("/deklaracja-dostepnosci")]
    public async Task<IActionResult> Deklaracja(CancellationToken ct) =>
        View(new DeklaracjaViewModel(
            deklaracja.Value,
            deklaracja.Value.AdresSerwisu ?? $"{Request.Scheme}://{Request.Host}/",
            await nawigacja.PobierzDostepnoscArchitektoniczna(ct),
            kontakt.Value));

    [HttpGet("/skroty-klawiszowe")]
    public IActionResult Skroty() => View();

    [HttpGet("/zglos-problem")]
    public IActionResult ZglosProblem() => View(new ZgloszenieFormularz());

    [HttpPost("/zglos-problem")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ZglosProblem(ZgloszenieFormularz model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await zgloszenia.Przyjmij(model.Tresc!, model.AdresStrony, model.ImieNazwisko, model.Email, ct);
        return RedirectToAction(nameof(ZgloszenieWyslane));
    }

    [HttpGet("/zglos-problem/wyslano")]
    public IActionResult ZgloszenieWyslane() => View(deklaracja.Value);

    [HttpGet("/polityka-prywatnosci")]
    public IActionResult PolitykaPrywatnosci() => View(deklaracja.Value);
}
