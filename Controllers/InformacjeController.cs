using Microsoft.AspNetCore.Mvc;

namespace PrzewodnikSWPW.Web.Controllers;

/// <summary>
/// Strony informacyjne ze stopki. Na razie z tekstem zastępczym — prawdziwa treść wymaga danych,
/// których jeszcze nie ma (opis dostępności budynków z bazy, wykaz skrótów, adres wdrożenia).
/// </summary>
public class InformacjeController : Controller
{
    [HttpGet("deklaracja-dostepnosci")]
    public IActionResult Deklaracja() => View();

    [HttpGet("skroty-klawiszowe")]
    public IActionResult Skroty() => View();

    [HttpGet("zglos-problem")]
    public IActionResult ZglosProblem() => View();

    [HttpGet("polityka-prywatnosci")]
    public IActionResult PolitykaPrywatnosci() => View();

    [HttpGet("ustawienia")]
    public IActionResult Ustawienia() => View();
}
