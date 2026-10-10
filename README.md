# Multimedialny przewodnik po Uczelni

Aplikacja internetowa prowadząca osoby niewidome i słabowidzące po budynkach Szkoły Wyższej
im. Pawła Włodkowica w Płocku: opis miejsca, wybór kierunku, wyszukiwanie sal i trasa z sali do sali.

## Wymagania

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQL Server — lokalnie wystarczy LocalDB (instaluje się z Visual Studio albo z SQL Server Express)

## Baza danych

Łańcucha połączenia nie ma w repozytorium. Każdy ustawia go u siebie w magazynie user-secrets
(plik poza katalogiem projektu); na serwerze służy do tego zmienna środowiskowa `ConnectionStrings__Default`.

```bash
dotnet user-secrets set "ConnectionStrings:Default" "Server=(localdb)\MSSQLLocalDB;Database=PrzewodnikSWPW;Trusted_Connection=True;TrustServerCertificate=True"
```

Bazę z tabelami i słownikami tworzy jedno polecenie:

```bash
dotnet tool restore
dotnet ef database update
```

Dane początkowe (zachodnia część parteru budynku A) wstawiają się same przy pierwszym uruchomieniu
aplikacji w środowisku deweloperskim; kolejne uruchomienia niczego nie dublują.

Bez łańcucha połączenia aplikacja nie wystartuje i wypisze komunikat odsyłający do tej sekcji.

## Konto administratora (tylko lokalnie)

Konto do panelu powstaje przy starcie w środowisku deweloperskim, z adresu i hasła zapisanych
w user-secrets. Hasła nie ma w kodzie ani w repozytorium; bez tych dwóch wartości konto nie powstaje.

```bash
dotnet user-secrets set "Przewodnik:AdministratorDev:Email" "admin@przewodnik.test"
dotnet user-secrets set "Przewodnik:AdministratorDev:Haslo" "<własne hasło>"
```

Hasło musi mieć co najmniej 12 znaków, wielką i małą literę, cyfrę i znak specjalny.
Zapisane wartości pokazuje `dotnet user-secrets list`.

## Uruchomienie lokalne

```bash
dotnet restore
dotnet run --launch-profile http
```

Aplikacja działa pod adresem `http://localhost:5082`.

## Co potrafi

- **Spacer po budynku** (`/spacer`): opis miejsca i cztery przejścia w stałej kolejności — prosto, w lewo,
  w prawo, do tyłu. Miejsce i zwrot są w adresie strony, więc działają przycisk Wstecz i zakładki.
- **Wyszukiwarka sal** (`/szukaj`): po symbolu, nazwie i innych nazwach sali; „a 15", „A-15" i „15" znajdują A15.
- **Trasa z sali do sali** (`/trasa`): najkrótsza droga w metrach, z trybem bez schodów.
- **Panel administratora** (`/admin`): budynki, piętra, sale, punkty ruchu, kierunki, zdjęcia, walidacja grafu.
- **Ustawienia dostępności** (`/ustawienia`): motyw, rozmiar tekstu, skróty klawiszowe i odczyt opisów głosem
  przeglądarki. Zapisywane w ciasteczku, bez konta.

Wszystko działa samą klawiaturą i przy wyłączonym JavaScripcie; JavaScript tylko usprawnia
(skróty `Alt+1`…`Alt+4` i `Alt+P`, podpowiedzi w wyszukiwarce, odczyt głosem).

## Panel administratora

Pod adresem `/admin` (link „Zaloguj się do panelu administratora" w nawigacji). Kolejność wprowadzania
nowego budynku:

1. **Budynki** — kod, nazwa, adres, opis dostępności architektonicznej.
2. **Piętra** — numer (0 = parter) i nazwa.
3. **Punkty ruchu** — miejsca, w których wybiera się kierunek: kod (np. `B-0-P01`), nazwa, opis i opis głosowy.
4. **Kierunki** każdego punktu — azymut względem planu budynku (0, 90, 180, 270), cel (punkt albo sala),
   odległość w metrach. Kierunek powrotny tworzy się razem z kierunkiem.
5. **Sale** — z punktem wejściowym przed drzwiami; potem kierunek „drzwi" z tego punktu do sali.
6. **Zdjęcia** punktów i sal — z tekstem alternatywnym, źródłem i licencją.
7. W edycji budynku wybierz **punkt wejścia głównego**.
8. **Walidacja grafu** — sprawdza, czy z każdego miejsca da się wrócić i czy do każdego da się dojść.

Rekordów się nie usuwa, tylko dezaktywuje. Każda zmiana trafia do rejestru zmian.

## Konfiguracja

Daty i dane kontaktowe Deklaracji Dostępności oraz kontakt do portierni są w sekcji `Przewodnik`
pliku `appsettings.json`. Puste wartości strona pokazuje jako „do uzupełnienia" — nie wymyśla ich.

| Ustawienie | Do czego służy | Domyślnie |
|---|---|---|
| `ConnectionStrings:Default` | łańcuch połączenia z bazą | brak — wymagany |
| `Zdjecia:KatalogMedia` | katalog wgrywanych zdjęć, serwowany pod `/media` | `wwwroot/media` |
| `Zdjecia:MaksRozmiarMB` | limit rozmiaru zdjęcia | 3 |
| `Przewodnik:KatalogLogow` | katalog dziennika zdarzeń | `logs` |
| `Przewodnik:KontaktAlternatywny` | opis, telefon i e-mail portierni | sam opis |

Na serwerze każde ustawienie podaje się zmienną środowiskową, z dwoma podkreśleniami zamiast dwukropka,
np. `Zdjecia__KatalogMedia`.

## Testy przed wydaniem

Przy każdym pull requeście GitHub Actions sprawdza kompilację bez ostrzeżeń i wykonanie migracji na pustej
bazie. Tego, czy przewodnik da się używać, automat nie sprawdzi. Automatyczne narzędzia do badania
dostępności (np. axe-core) wykrywają około 30–40% problemów i **nie zastępują** testu z czytnikiem ekranu
ani testu z osobą niewidomą. Przed każdym wydaniem przejdź tę listę:

1. **Odłóż mysz.** Przejdź całą ścieżkę: wejście → wyszukanie sali A15 → trasa → spacer → powrót.
   Wyłącznie Tab, Shift+Tab, Enter, spacja, Esc.
2. Czy widać, który element ma fokus — na każdym kroku?
3. Czy kolejność tabulacji odpowiada kolejności na ekranie?
4. Czy jakiś element nie blokuje nawigacji (pułapka fokusa)?
5. **Wyłącz monitor** (albo włącz kurtynę ekranu w NVDA: `NVDA+Ctrl+Escape`) i przejdź tę samą ścieżkę, słuchając.
6. Powiększ stronę do 200% i 400% — czy wszystko jest osiągalne?
7. Przełącz na motyw wysokiego kontrastu — czy każdy element jest nadal widoczny?
8. Wyłącz JavaScript — czy spacer, wyszukiwanie i trasa nadal działają?
9. W panelu uruchom **Walidację grafu** — bez błędów krytycznych.

## Licencja

[MIT](LICENSE)
