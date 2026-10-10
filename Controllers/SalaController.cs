using Microsoft.AspNetCore.Mvc;
using PrzewodnikSWPW.Web.Services;
using PrzewodnikSWPW.Web.ViewModels;

namespace PrzewodnikSWPW.Web.Controllers;

/// <summary>Karta sali — cel spaceru, wyszukiwania i trasy.</summary>
[Route("sala")]
public class SalaController(NawigacjaService nawigacja) : Controller
{
    /// <summary>
    /// <paramref name="zPunktu"/> i <paramref name="zwrot"/> mówią, skąd i w którą stronę użytkownik wszedł —
    /// link powrotu prowadzi na ten punkt ze zwrotem odwróconym (twarzą od drzwi).
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Index(int id, int? zPunktu, int? zwrot, CancellationToken ct)
    {
        WidokSali? sala;
        try
        {
            sala = await nawigacja.PobierzSale(id, zPunktu, zwrot, ct);
        }
        catch (ArgumentOutOfRangeException)
        {
            return BadRequest(); // zwrot w adresie nie jest wielokrotnością 90
        }

        if (sala is null)
        {
            return NotFound();
        }

        var powrot = sala is { Powrot: { } punkt, ZwrotPowrotu: int zwrotPowrotu }
            ? new LinkDoMiejsca(punkt.Nazwa, Url.Action(nameof(SpacerController.Miejsce), "Spacer", new
            {
                kodBudynku = punkt.Adres.KodBudynku,
                pietro = punkt.Adres.NumerPietra,
                kodPunktu = punkt.Adres.SegmentPunktu,
                zwrot = zwrotPowrotu,
            })!)
            : null;

        return View(new SalaViewModel(
            sala.Id, sala.Symbol, sala.Nazwa, sala.TypSali, sala.Opis ?? sala.OpisGlosowy, sala.OpisGlosowy ?? sala.Opis,
            sala.NazwaBudynku, sala.NazwaPietra, sala.LiczbaMiejsc, sala.CzyDostepnaDlaWozkow,
            sala.Zdjecia.Select(z => ZdjecieNaEkranie.Z(z, $"Zdjęcie sali {sala.Symbol}.")).ToList(),
            sala.Udogodnienia.Select(u => u.Uwagi is null ? u.Nazwa : $"{u.Nazwa} — {u.Uwagi}").ToList(),
            powrot));
    }
}
