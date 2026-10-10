using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.Web.Areas.Admin.Controllers;

[Route("admin")]
public class PanelController(AdministracjaService admin, WalidatorGrafuService walidator) : AdminKontroler
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
}
