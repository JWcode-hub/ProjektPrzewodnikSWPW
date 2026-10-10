using Microsoft.AspNetCore.Mvc;
using PrzewodnikSWPW.Web.Services;
using PrzewodnikSWPW.Web.ViewModels;

namespace PrzewodnikSWPW.Web.Controllers;

/// <summary>
/// Spacer po budynku. Cały stan — punkt i zwrot — jest w adresie strony, więc działają przycisk Wstecz,
/// zakładki i wysłanie linku. Działa bez JavaScriptu: kierunki to zwykłe linki, a przejście to żądanie
/// GET z przekierowaniem na adres nowego miejsca.
/// </summary>
[Route("spacer")]
public class SpacerController(NawigacjaService nawigacja) : Controller
{
    private const string KluczKomunikatu = "KomunikatSpaceru";

    /// <summary>Wartości parametru „kierunek” w adresie /spacer/idz.</summary>
    public static readonly IReadOnlyDictionary<KierunekWzgledny, string> ParametrKierunku =
        new Dictionary<KierunekWzgledny, string>
        {
            [KierunekWzgledny.Prosto] = "prosto",
            [KierunekWzgledny.WLewo] = "lewo",
            [KierunekWzgledny.WPrawo] = "prawo",
            [KierunekWzgledny.DoTylu] = "tyl",
        };

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(new BudynkiViewModel(await nawigacja.PobierzBudynki(ct)));

    /// <summary>Przejście jedną krawędzią i przekierowanie na adres nowego miejsca.</summary>
    [HttpGet("idz")]
    public async Task<IActionResult> Idz(int zPunktu, string? kierunek, int zwrot, CancellationToken ct)
    {
        var wzgledny = ParametrKierunku
            .Where(p => string.Equals(p.Value, kierunek, StringComparison.OrdinalIgnoreCase))
            .Select(p => (KierunekWzgledny?)p.Key)
            .SingleOrDefault();
        if (wzgledny is null || !CzyPoprawnyZwrot(zwrot))
        {
            return BadRequest();
        }

        var wynik = await nawigacja.Przejdz(zPunktu, wzgledny.Value, zwrot, ct);
        if (wynik is null)
        {
            return NotFound();
        }

        switch (wynik.Rodzaj)
        {
            case RodzajWynikuPrzejscia.DoSali:
                return RedirectToAction(nameof(SalaController.Index), "Sala",
                    new { id = wynik.SalaId, zPunktu = wynik.PunktId, zwrot = wynik.Zwrot });

            case RodzajWynikuPrzejscia.BrakPrzejscia:
                TempData[KluczKomunikatu] = wynik.Opis;
                break;
        }

        return Redirect(AdresMiejsca(wynik.Adres, wynik.Zwrot));
    }

    [HttpGet("{kodBudynku}")]
    public async Task<IActionResult> Budynek(string kodBudynku, CancellationToken ct)
    {
        var budynek = await nawigacja.PobierzBudynek(kodBudynku, ct);
        if (budynek is null)
        {
            return NotFound();
        }

        LinkDoMiejsca Link(PunktNaLiscie punkt) => new(punkt.Nazwa, AdresMiejsca(punkt.Adres, punkt.AzymutDomyslny));

        return View(new BudynekViewModel(
            budynek.Kod, budynek.Nazwa, budynek.Adres, budynek.OpisDostepnosciArchitektonicznej,
            budynek.PunktStartowy is { } start ? Link(start) : null,
            budynek.Pietra.Select(p => new PietroViewModel(p.Nazwa, p.Punkty.Select(Link).ToList())).ToList()));
    }

    /// <summary>
    /// Ekran miejsca. Adres bez zwrotu jest przekierowywany na adres z domyślnym zwrotem punktu;
    /// zwrot, który nie jest wielokrotnością 90, to błędny adres.
    /// </summary>
    [HttpGet("{kodBudynku}/{pietro:int}/{kodPunktu}")]
    public async Task<IActionResult> Miejsce(string kodBudynku, int pietro, string kodPunktu, string? zwrot, CancellationToken ct)
    {
        var punktId = await nawigacja.ZnajdzPunkt(kodBudynku, pietro, kodPunktu, ct);
        if (punktId is null)
        {
            return NotFound();
        }

        if (string.IsNullOrEmpty(zwrot))
        {
            var domyslny = await nawigacja.DomyslnyZwrot(punktId.Value, ct);
            return domyslny is null
                ? NotFound()
                : RedirectToAction(nameof(Miejsce), new { kodBudynku, pietro, kodPunktu, zwrot = domyslny });
        }

        if (!int.TryParse(zwrot, out var zwrotLiczba) || !CzyPoprawnyZwrot(zwrotLiczba))
        {
            return BadRequest();
        }

        var widok = await nawigacja.PobierzMiejsce(punktId.Value, zwrotLiczba, ct);
        if (widok is null)
        {
            return NotFound();
        }

        var kierunki = widok.Kierunki
            .Select(k => new KierunekNaEkranie(
                ParametrKierunku[k.Kierunek],
                k.Nazwa,
                k.Szczegol,
                k.CzyMozliwy
                    ? Url.Action(nameof(Idz), new { zPunktu = widok.PunktId, kierunek = ParametrKierunku[k.Kierunek], zwrot = widok.Zwrot })
                    : null))
            .ToList();

        // Tekst do odczytu głosem: opis głosowy, a po nim notatki o dojściu możliwych przejść, w kolejności
        // listy, każda poprzedzona nazwą przejścia.
        var doPrzeczytania = string.Join(" ", new[] { widok.OpisGlosowy ?? widok.Opis }
            .Concat(widok.Kierunki.Where(k => k.CzyMozliwy && k.Notatka is not null).Select(k => $"{k.Nazwa}. {k.Notatka}"))
            .Where(t => !string.IsNullOrWhiteSpace(t)));

        return View(new MiejsceViewModel(
            widok.Nazwa, widok.Opis ?? widok.OpisGlosowy, doPrzeczytania.Length == 0 ? null : doPrzeczytania, widok.NazwaBudynku, widok.NazwaPietra, widok.Adres.KodBudynku,
            Url.Action(nameof(Budynek), new { kodBudynku = widok.Adres.KodBudynku })!,
            widok.Zdjecia.Select(z => ZdjecieNaEkranie.Z(z, $"Zdjęcie miejsca: {widok.Nazwa}.")).ToList(),
            kierunki,
            TempData[KluczKomunikatu] as string));
    }

    // Sprawdzenie przez Azymuty, żeby reguła „wielokrotność 90” była zapisana w jednym miejscu.
    private static bool CzyPoprawnyZwrot(int zwrot)
    {
        try
        {
            Azymuty.Normalizuj(zwrot);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private string AdresMiejsca(AdresMiejsca adres, int zwrot) =>
        Url.Action(nameof(Miejsce), "Spacer", new
        {
            kodBudynku = adres.KodBudynku,
            pietro = adres.NumerPietra,
            kodPunktu = adres.SegmentPunktu,
            zwrot = Azymuty.Normalizuj(zwrot),
        })!;
}
