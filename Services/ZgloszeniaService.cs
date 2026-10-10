using PrzewodnikSWPW.Web.Data;
using PrzewodnikSWPW.Web.Models;

namespace PrzewodnikSWPW.Web.Services;

/// <summary>
/// Przyjmowanie zgłoszeń braku dostępności. Wymagana jest wyłącznie treść — zgłoszenie anonimowe
/// musi być możliwe, więc puste pola opcjonalne zapisujemy jako brak wartości, nie jako pusty tekst.
/// </summary>
public sealed class ZgloszeniaService(IZgloszeniaRepozytorium repozytorium)
{
    public Task Przyjmij(string tresc, string? adresStrony, string? imieNazwisko, string? email, CancellationToken ct = default) =>
        repozytorium.DodajAsync(new ZgloszenieDostepnosci
        {
            Tresc = tresc.Trim(),
            AdresStrony = Przytnij(adresStrony),
            ImieNazwisko = Przytnij(imieNazwisko),
            Email = Przytnij(email),
        }, ct);

    private static string? Przytnij(string? tekst) => string.IsNullOrWhiteSpace(tekst) ? null : tekst.Trim();
}
