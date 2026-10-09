using Microsoft.AspNetCore.Mvc;

namespace PrzewodnikSWPW.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();
}
