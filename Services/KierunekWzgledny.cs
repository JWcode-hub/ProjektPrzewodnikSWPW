namespace PrzewodnikSWPW.Web.Services;

/// <summary>
/// Kierunek względem aktualnego zwrotu użytkownika. Wartość = przesunięcie w stopniach zgodnie
/// z ruchem wskazówek zegara. NIGDY nie jest zapisywany w bazie — wylicza go <see cref="Azymuty"/>.
/// </summary>
public enum KierunekWzgledny
{
    Prosto = 0,
    WPrawo = 90,
    DoTylu = 180,
    WLewo = 270,
}
