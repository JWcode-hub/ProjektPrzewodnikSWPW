using System.ComponentModel.DataAnnotations;

namespace PrzewodnikSWPW.Web.Models;

/// <summary>Czasowa blokada kierunku albo punktu ruchu (remont, awaria windy) z datami obowiązywania.</summary>
public class Utrudnienie
{
    public int Id { get; set; }

    [Display(Name = "Zablokowany kierunek")]
    public int? KierunekId { get; set; }
    public Kierunek? Kierunek { get; set; }

    [Display(Name = "Zablokowany punkt ruchu")]
    public int? PunktRuchuId { get; set; }
    public PunktRuchu? PunktRuchu { get; set; }

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [StringLength(400, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Przyczyna utrudnienia")]
    public string Przyczyna { get; set; } = string.Empty;

    [Required(ErrorMessage = Komunikaty.Wymagane)]
    [DataType(DataType.DateTime)]
    [Display(Name = "Obowiązuje od")]
    public DateTime ObowiazujeOd { get; set; }

    /// <summary><c>null</c> = bezterminowo.</summary>
    [DataType(DataType.DateTime)]
    [Display(Name = "Obowiązuje do (puste = bezterminowo)")]
    public DateTime? ObowiazujeDo { get; set; }

    /// <summary>Identyfikator użytkownika ASP.NET Core Identity (AspNetUsers.Id).</summary>
    [StringLength(450, ErrorMessage = Komunikaty.MaksDlugosc)]
    [Display(Name = "Utworzone przez")]
    public string? UtworzylId { get; set; }

    [Display(Name = "Data utworzenia")]
    public DateTime DataUtworzenia { get; set; }
}
