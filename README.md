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

Bez łańcucha połączenia aplikacja nie wystartuje i wypisze komunikat odsyłający do tej sekcji.

## Uruchomienie lokalne

```bash
dotnet restore
dotnet run --launch-profile http
```

Aplikacja działa pod adresem `http://localhost:5082`.

## Licencja

[MIT](LICENSE)
