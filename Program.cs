using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using PrzewodnikSWPW.Web.Data;

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

app.Run();

// Potrzebne testom: WebApplicationFactory<Program> wymaga publicznego typu.
public partial class Program;
