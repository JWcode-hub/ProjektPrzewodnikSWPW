namespace PrzewodnikSWPW.Web.Services;

public enum StatusZgodnosci { Zgodna, CzesciowoZgodna, Niezgodna }

/// <summary>
/// Dane Deklaracji Dostępności (art. 10 ustawy o dostępności cyfrowej) — w konfiguracji, nie w widoku,
/// bo zmieniają się przy każdym przeglądzie i po wdrożeniu. Brakujące daty i dane kontaktowe strona
/// oznacza jako do uzupełnienia, zamiast je wymyślać.
/// </summary>
public sealed class DeklaracjaDostepnosciOptions
{
    public const string Sekcja = "Przewodnik:Deklaracja";

    public string NazwaPodmiotu { get; set; } = "Szkoła Wyższa im. Pawła Włodkowica w Płocku";

    /// <summary>Adres serwisu; gdy pusty — adres, pod którym strona została otwarta.</summary>
    public string? AdresSerwisu { get; set; }

    public DateOnly? DataPublikacji { get; set; }
    public DateOnly? DataAktualizacji { get; set; }
    public DateOnly? DataSporzadzenia { get; set; }

    public StatusZgodnosci Status { get; set; } = StatusZgodnosci.CzesciowoZgodna;

    /// <summary>Znane niezgodności — wymagane przy statusie innym niż „zgodna”.</summary>
    public string[] Niezgodnosci { get; set; } = [];

    public string SposobOceny { get; set; } = "samoocena";
    public string? Audytor { get; set; }

    public string? OsobaKontaktowa { get; set; }
    public string? Email { get; set; }
    public string? Telefon { get; set; }

    /// <summary>Informacja o tłumaczu polskiego języka migowego (art. 10 ust. 4 pkt 7).</summary>
    public string? TlumaczMigowy { get; set; }

    public string OpisStatusu => Status switch
    {
        StatusZgodnosci.Zgodna => "w pełni zgodna",
        StatusZgodnosci.CzesciowoZgodna => "częściowo zgodna",
        _ => "niezgodna",
    };
}
