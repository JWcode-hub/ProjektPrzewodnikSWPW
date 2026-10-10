using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PrzewodnikSWPW.Web.Models;
using PrzewodnikSWPW.Web.Services;
using PrzewodnikSWPW.Web.ViewModels.Admin;

namespace PrzewodnikSWPW.Web.Areas.Admin.Controllers;

/// <summary>Punkty ruchu. Zamiast usuwania — CzyAktywny = false.</summary>
[Route("admin/punkty")]
public class PunktyController(AdministracjaService admin, IMagazynZdjec magazyn) : AdminKontroler
{
    [Authorize(Roles = Role.Administrator)]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        PrzekazKomunikat();
        return View((await admin.Punkty(ct)).Select(PunktWiersz.Z).ToList());
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("nowy")]
    public async Task<IActionResult> Nowy(CancellationToken ct) => View("Formularz", await Uzupelnij(new PunktFormularz(), ct));

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("nowy")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nowy(PunktFormularz model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var punkt = model.NaEncje(new PunktRuchu());
            var wynik = await admin.ZapiszPunkt(punkt, ct);
            if (wynik.Sukces)
            {
                Komunikat($"Dodano punkt {model.Kod}. Teraz dodaj jego kierunki.");
                return RedirectToAction(nameof(KierunkiController.Index), "Kierunki", new { punkt = punkt.Id });
            }
            DodajBledy(wynik);
        }
        return View("Formularz", await Uzupelnij(model, ct));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("{id:int}/edytuj")]
    public async Task<IActionResult> Edytuj(int id, CancellationToken ct) =>
        await admin.PobierzPunkt(id, ct) is { } p ? View("Formularz", await Uzupelnij(PunktFormularz.Z(p), ct)) : NotFound();

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("{id:int}/edytuj")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edytuj(int id, PunktFormularz model, CancellationToken ct)
    {
        if (await admin.PobierzPunkt(id, ct) is not { } p) return NotFound();
        model.Id = id;
        if (ModelState.IsValid)
        {
            var wynik = await admin.ZapiszPunkt(model.NaEncje(p), ct);
            if (wynik.Sukces)
            {
                Komunikat($"Zapisano zmiany w punkcie {model.Kod}.");
                return RedirectToAction(nameof(Index));
            }
            DodajBledy(wynik);
        }
        return View("Formularz", await Uzupelnij(model, ct));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("{id:int}/dezaktywuj")]
    public async Task<IActionResult> Dezaktywuj(int id, CancellationToken ct)
    {
        if (await admin.PobierzPunkt(id, ct) is not { } p) return NotFound();
        return View("Potwierdzenie", new PotwierdzenieViewModel(
            $"Dezaktywacja punktu {p.Kod}",
            $"Czy na pewno dezaktywować punkt {p.Kod} — {p.Nazwa}?",
            "Punkt zniknie ze spaceru, a trasy przestaną przez niego prowadzić. Punkt i jego kierunki zostaną w bazie — możesz go później przywrócić.",
            null,
            $"Tak, dezaktywuj punkt {p.Kod}",
            Url.Action(nameof(Dezaktywuj), new { id })!,
            Url.Action(nameof(Index))!,
            "Anuluj i wróć do listy punktów"));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("{id:int}/dezaktywuj")]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(Dezaktywuj))]
    public async Task<IActionResult> DezaktywujPotwierdzone(int id, CancellationToken ct)
    {
        var wynik = await admin.UstawAktywnoscPunktu(id, false, ct);
        Komunikat(wynik.Sukces ? "Punkt dezaktywowano." : wynik.Bledy[0].Komunikat);
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpGet("{id:int}/usun")]
    public async Task<IActionResult> Usun(int id, CancellationToken ct)
    {
        if (await admin.SkutkiUsunieciaPunktu(id, ct) is not { } s) return NotFound();

        var skutki = new List<string>();
        if (s.KierunkiWychodzace > 0)
        {
            skutki.Add($"Znikną kierunki wychodzące z tego punktu: {s.KierunkiWychodzace}.");
        }
        if (s.PunktyZPrzejsciemTutaj.Count > 0)
        {
            skutki.Add($"Znikną kierunki prowadzące tutaj z punktów: {string.Join(", ", s.PunktyZPrzejsciemTutaj)}. " +
                       "W tych punktach zostanie w tym miejscu „brak przejścia” bez opisu.");
        }
        if (s.SaleTracaceWejscie.Count > 0)
        {
            skutki.Add($"Sale zostaną bez punktu wejściowego: {string.Join(", ", s.SaleTracaceWejscie)}. " +
                       "Dopóki nie wskażesz im nowego, nie da się do nich wyznaczyć trasy.");
        }
        if (s.Zdjecia > 0)
        {
            skutki.Add($"Znikną zdjęcia tego punktu razem z plikami: {s.Zdjecia}.");
        }

        return View("Potwierdzenie", new PotwierdzenieViewModel(
            $"Usunięcie punktu {s.Kod}",
            $"Czy na pewno usunąć punkt {s.Kod} — {s.Nazwa}?",
            "Punkt zniknie z bazy i nie da się go przywrócić. Jeśli chcesz go tylko ukryć, wybierz dezaktywację.",
            s.WejscieGlowneBudynku is null
                ? null
                : $"Punkt {s.Kod} jest wejściem głównym budynku {s.WejscieGlowneBudynku}. Najpierw wybierz inne wejście główne w edycji budynku.",
            $"Tak, usuń punkt {s.Kod}",
            Url.Action(nameof(Usun), new { id })!,
            Url.Action(nameof(Index))!,
            "Anuluj i wróć do listy punktów",
            Skutki: skutki));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("{id:int}/usun")]
    [ValidateAntiForgeryToken]
    [ActionName(nameof(Usun))]
    public async Task<IActionResult> UsunPotwierdzone(int id, CancellationToken ct)
    {
        var (wynik, pliki) = await admin.UsunPunkt(id, ct);
        // Pliki kasujemy dopiero po zatwierdzeniu zmian w bazie — rekord bez pliku byłby gorszy niż plik bez rekordu.
        foreach (var plik in pliki)
        {
            magazyn.Usun(plik);
        }
        Komunikat(wynik.Sukces ? "Punkt usunięto. Sprawdź w spacerze połączenia sąsiednich miejsc." : wynik.Bledy[0].Komunikat);
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Role.Administrator)]
    [HttpPost("{id:int}/aktywuj")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aktywuj(int id, CancellationToken ct)
    {
        var wynik = await admin.UstawAktywnoscPunktu(id, true, ct);
        Komunikat(wynik.Sukces ? "Punkt przywrócono." : wynik.Bledy[0].Komunikat);
        return RedirectToAction(nameof(Index));
    }

    private async Task<PunktFormularz> Uzupelnij(PunktFormularz model, CancellationToken ct)
    {
        model.Pietra = Lista(await admin.Pietra(ct), p => p.Id, p => $"{p.Budynek.Kod}: {p.Nazwa}");
        model.TypyPunktow = Lista(await admin.Slownik<TypPunktu>(ct), t => t.Id, t => t.Nazwa);
        return model;
    }
}
