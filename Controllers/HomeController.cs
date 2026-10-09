using Microsoft.AspNetCore.Mvc;

namespace PrzewodnikSWPW.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();

    /// <summary>
    /// Strona błędu dla kodów stanu i nieobsłużonych wyjątków. Kod odpowiedzi zostaje ten sam
    /// (np. 404) — zmienia się tylko treść: zamiast pustej odpowiedzi strona z nawigacją przewodnika.
    /// </summary>
    [Route("blad/{kod:int}")]
    public IActionResult Blad(int kod) => View(kod == 404 ? "NieZnaleziono" : "Blad");
}
