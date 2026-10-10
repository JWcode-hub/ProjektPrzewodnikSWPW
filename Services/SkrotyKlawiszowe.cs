namespace PrzewodnikSWPW.Web.Services;

/// <summary>JEDNA lista skrótów — strona /skroty-klawiszowe i Deklaracja Dostępności korzystają z tej samej listy.</summary>
public static class SkrotyKlawiszowe
{
    public sealed record Skrot(string Klawisze, string Dzialanie, string GdzieDziala);

    public static readonly IReadOnlyList<Skrot> Wszystkie =
    [
        new("Alt + 1", "Idź prosto", "ekran miejsca w spacerze"),
        new("Alt + 2", "Skręć w lewo", "ekran miejsca w spacerze"),
        new("Alt + 3", "Skręć w prawo", "ekran miejsca w spacerze"),
        new("Alt + 4", "Zawróć", "ekran miejsca w spacerze"),
        new("Alt + P", "Powtórz opis miejsca (odczytuje go czytnik ekranu, a po włączeniu mowy w ustawieniach — głos przeglądarki)", "ekran miejsca w spacerze"),
    ];
}
