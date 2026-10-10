using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrzewodnikSWPW.Web.Services;
using PrzewodnikSWPW.Web.ViewModels.Admin;

namespace PrzewodnikSWPW.Web.Areas.Admin.Controllers;

[Route("admin")]
public class PanelController(AdministracjaService admin, WalidatorGrafuService walidator, DaneBudynkowService dane) : AdminKontroler
{
    [Authorize(Roles = Role.Administrator)]
    [HttpGet("")]
    public IActionResult Index() => View();

    /// <summary>Rejestr zmian — ostatnie wpisy.</summary>
    [Authorize(Roles = Role.Administrator)]
    [HttpGet("rejestr-zmian")]
    public async Task<IActionResult> RejestrZmian(CancellationToken ct) => View(await admin.OstatnieWpisyAudytu(100, ct));

    /// <summary>Walidator grafu — raport z linkami do edycji. Uruchamiać przed każdym wdrożeniem.</summary>
    [Authorize(Roles = Role.Administrator)]
    [HttpGet("walidacja-grafu")]
    public async Task<IActionResult> WalidacjaGrafu(CancellationToken ct) => View(await walidator.WalidujAsync(ct));

    /// <summary>
    /// Zapis danych budynków do pliku w repozytorium. Baza leży poza katalogiem projektu, więc dopiero
    /// ten plik (i katalog zdjęć) da się zatwierdzić w gicie i przenieść na inny komputer.
    /// </summary>
    [Authorize(Roles = Role.Administrator)]
    [HttpGet("dane")]
    public async Task<IActionResult> Dane(CancellationToken ct)
    {
        PrzekazKomunikat();
        return View(new DaneViewModel(dane.SciezkaPliku, dane.DataPliku,
            DaneBudynkowService.Podsumuj(await dane.OdczytajZBazyAsync(ct))));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("dane")]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(Dane))]
    public async Task<IActionResult> ZapiszDane(CancellationToken ct)
    {
        var zapisane = await dane.ZapiszDoPlikuAsync(ct);
        Komunikat($"Zapisano dane do pliku: budynki {zapisane.Budynki}, punkty ruchu {zapisane.Punkty}, " +
                  $"kierunki {zapisane.Kierunki}, sale {zapisane.Sale}, zdjęcia {zapisane.Zdjecia}.");
        return RedirectToAction(nameof(Dane));
    }
}
