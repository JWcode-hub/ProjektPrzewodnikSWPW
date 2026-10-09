namespace PrzewodnikSWPW.Web.Services;

/// <summary>
/// JEDYNE miejsce w projekcie, które przelicza azymut bezwzględny na kierunek względny i odwrotnie.
/// W bazie jest azymut budynku (0/90/180/270), a użytkownik dostaje „w lewo” albo „w prawo” zależnie
/// od tego, skąd przyszedł. Pomyłka tutaj prowadzi osobę niewidomą w ścianę.
///
///   zwrot \ azymut |    0     |    90    |   180    |   270
///   ---------------+----------+----------+----------+----------
///          0       | prosto   | w prawo  | do tyłu  | w lewo
///         90       | w lewo   | prosto   | w prawo  | do tyłu
///        180       | do tyłu  | w lewo   | prosto   | w prawo
///        270       | w prawo  | do tyłu  | w lewo   | prosto
///
/// Po przejściu krawędzią nowy zwrot użytkownika = azymut przebytej krawędzi.
/// </summary>
public static class Azymuty
{
    /// <summary>Kolejność kierunków na każdym ekranie — jedyna definicja; nigdy nie zależy od danych.</summary>
    public static readonly IReadOnlyList<KierunekWzgledny> KolejnoscNaLiscie =
    [
        KierunekWzgledny.Prosto,
        KierunekWzgledny.WLewo,
        KierunekWzgledny.WPrawo,
        KierunekWzgledny.DoTylu,
    ];

    /// <summary>
    /// kierunekWzgledny = (azymutKrawedzi − zwrotUzytkownika) mod 360
    /// 0 → prosto · 90 → w prawo · 180 → do tyłu · 270 → w lewo
    /// </summary>
    public static KierunekWzgledny NaWzgledny(int azymutKrawedzi, int zwrotUzytkownika) =>
        (KierunekWzgledny)Normalizuj(azymutKrawedzi - zwrotUzytkownika);

    /// <summary>Odwrotność: azymut = (przesunięcie + zwrotUzytkownika) mod 360.</summary>
    public static int NaBezwzgledny(KierunekWzgledny wzgledny, int zwrotUzytkownika) =>
        Normalizuj((int)wzgledny + zwrotUzytkownika);

    /// <summary>
    /// Sprowadza kąt do [0, 360) i pilnuje, żeby był wielokrotnością 90.
    ///
    /// PODWÓJNE MODULO jest konieczne: w C# operator % zachowuje znak dzielnej,
    /// więc (0 − 90) % 360 == −90, a nie 270. Samo „% 360” dałoby dla azymutu 0 i zwrotu 90
    /// wartość −90, której nie ma w enumie — rzutowanie przeszłoby bez błędu, a ekran
    /// nie pokazałby żadnego kierunku albo pokazałby zły. Drugie „+ 360) % 360” to naprawia.
    /// </summary>
    public static int Normalizuj(int stopnie)
    {
        var wynik = ((stopnie % 360) + 360) % 360;

        // Azymut 45 albo 91 to błąd danych lub adresu URL. Wolimy wyjątek teraz
        // niż „prawie prosto” zaokrąglone po cichu do złego kierunku.
        if (wynik % 90 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stopnie), stopnie,
                "Azymut musi być wielokrotnością 90 stopni (0, 90, 180, 270).");
        }

        return wynik;
    }

    /// <summary>Etykieta czynności — zawsze zaczyna się od czasownika, bez stopni i kierunków świata.</summary>
    public static string Etykieta(KierunekWzgledny kierunek) => kierunek switch
    {
        KierunekWzgledny.Prosto => "Idź prosto",
        KierunekWzgledny.WLewo => "Skręć w lewo",
        KierunekWzgledny.WPrawo => "Skręć w prawo",
        KierunekWzgledny.DoTylu => "Zawróć",
        _ => throw new ArgumentOutOfRangeException(nameof(kierunek), kierunek, null),
    };

    /// <summary>Nazwa strony dla kierunku, którym nie da się przejść („W lewo — brak przejścia.”).</summary>
    public static string NazwaStrony(KierunekWzgledny kierunek) => kierunek switch
    {
        KierunekWzgledny.Prosto => "Prosto",
        KierunekWzgledny.WLewo => "W lewo",
        KierunekWzgledny.WPrawo => "W prawo",
        KierunekWzgledny.DoTylu => "Do tyłu",
        _ => throw new ArgumentOutOfRangeException(nameof(kierunek), kierunek, null),
    };
}
