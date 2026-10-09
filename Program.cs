using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PrzewodnikSWPW.Web.Data;
using PrzewodnikSWPW.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Łańcucha połączenia nie ma w repozytorium — pochodzi z user-secrets albo zmiennej środowiskowej.
var lancuchPolaczenia = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(lancuchPolaczenia))
{
    throw new InvalidOperationException(
        "Brak łańcucha połączenia z bazą danych (ConnectionStrings:Default). " +
        "Ustaw go poleceniem „dotnet user-secrets set” — instrukcja w README.md, sekcja „Baza danych”.");
}

builder.Services.AddDbContext<PrzewodnikDbContext>(opcje => opcje.UseSqlServer(lancuchPolaczenia));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ISpacerRepozytorium, SpacerRepozytorium>();
builder.Services.AddScoped<NawigacjaService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/blad/500");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/blad/{0}");

app.UseHttpsRedirection();

var kultura = new CultureInfo("pl-PL");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(kultura),
    SupportedCultures = [kultura],
    SupportedUICultures = [kultura],
});

app.UseRouting();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Dane początkowe budynku A — wyłącznie w środowisku deweloperskim. Gdy baza nie istnieje,
// aplikacja i tak startuje: strony bez zapytań do bazy działają, a w logu jest wskazówka.
if (app.Environment.IsDevelopment())
{
    using var zakres = app.Services.CreateScope();
    try
    {
        await DbSeeder.ZasiejAsync(zakres.ServiceProvider.GetRequiredService<PrzewodnikDbContext>());
    }
    // Brak bazy albo tabel: EF zgłasza SqlException bezpośrednio albo opakowany. Błąd w samych danych
    // (DbUpdateException) nie jest tu łapany — ma zatrzymać start.
    catch (Exception blad) when (blad is SqlException || blad.InnerException is SqlException)
    {
        app.Logger.LogError(blad,
            "Nie udało się wstawić danych początkowych. Sprawdź, czy baza istnieje: „dotnet ef database update”.");
    }
}

app.Run();

// Potrzebne testom: WebApplicationFactory<Program> wymaga publicznego typu.
public partial class Program;
