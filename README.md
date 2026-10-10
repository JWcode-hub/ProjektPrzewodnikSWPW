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
- **Ustawienia dostępności** (`/ustawienia`): motyw, rozmiar tekstu, skróty klawiszowe i odczyt opisów głosem
  przeglądarki. Zapisywane w ciasteczku, bez konta.

Wszystko działa samą klawiaturą i przy wyłączonym JavaScripcie; JavaScript tylko usprawnia
(skróty `Alt+1`…`Alt+4` i `Alt+P`, podpowiedzi w wyszukiwarce, odczyt głosem).

## Konfiguracja

Daty i dane kontaktowe Deklaracji Dostępności oraz kontakt do portierni są w sekcji `Przewodnik`
pliku `appsettings.json`. Puste wartości strona pokazuje jako „do uzupełnienia" — nie wymyśla ich.

## Licencja

[MIT](LICENSE)
