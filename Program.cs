using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.WebEncoders;
using PrzewodnikSWPW.Web.Data;
using PrzewodnikSWPW.Web.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Dziennik zdarzeń: konsola i plik dzienny, przechowywany 14 dni. Bez danych osobowych — nie zapisujemy
// treści formularzy, adresów IP ani nazw kont; żądania platformy logujemy dopiero od poziomu ostrzeżeń.
var katalogLogow = Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["Przewodnik:KatalogLogow"] ?? "logs");
builder.Services.AddSerilog(log => log
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .WriteTo.File(Path.Combine(katalogLogow, "przewodnik-.log"), rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14, formatProvider: CultureInfo.InvariantCulture));

builder.Services.AddControllersWithViews(o =>
{
    // Autoryzacja całego obszaru Admin — niezależnie od atrybutów na kontrolerach i akcjach.
    o.Conventions.Add(new AutoryzacjaObszaruAdmin());

    // Komunikaty wiązania modelu po polsku — domyślne są angielskie („The value 'x' is not valid”).
    var k = o.ModelBindingMessageProvider;
    k.SetValueIsInvalidAccessor(w => $"Wartość „{w}” jest nieprawidłowa.");
    k.SetAttemptedValueIsInvalidAccessor((w, pole) => $"Wartość „{w}” w polu „{pole}” jest nieprawidłowa.");
    k.SetValueMustBeANumberAccessor(pole => $"Pole „{pole}” musi być liczbą. Część dziesiętną oddziel przecinkiem, np. 5,5.");
    k.SetValueMustNotBeNullAccessor(pole => $"Pole „{pole}” jest wymagane.");
    k.SetMissingBindRequiredValueAccessor(pole => $"Brak wartości pola „{pole}”.");
    k.SetMissingKeyOrValueAccessor(() => "Brak wymaganej wartości.");
    k.SetMissingRequestBodyRequiredValueAccessor(() => "Brak danych formularza.");
    k.SetNonPropertyAttemptedValueIsInvalidAccessor(w => $"Wartość „{w}” jest nieprawidłowa.");
    k.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Podana wartość jest nieprawidłowa.");
    k.SetNonPropertyValueMustBeANumberAccessor(() => "Wartość musi być liczbą.");
    k.SetUnknownValueIsInvalidAccessor(pole => $"Wartość w polu „{pole}” jest nieprawidłowa.");
});

// Polskie znaki w HTML jako znaki, nie encje (&#x17A;). Znaki specjalne HTML (<, >, &, ") nadal są kodowane.
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

// Łańcucha połączenia nie ma w repozytorium — pochodzi z user-secrets albo zmiennej środowiskowej.
var lancuchPolaczenia = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(lancuchPolaczenia))
{
    throw new InvalidOperationException(
        "Brak łańcucha połączenia z bazą danych (ConnectionStrings:Default). " +
        "Ustaw go poleceniem „dotnet user-secrets set” — instrukcja w README.md, sekcja „Baza danych”.");
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);

// Każdy zapis przechodzi przez dwa interceptory: rejestr zmian (kto, kiedy, co) i unieważnienie
// grafu budynku w pamięci po zmianie punktów, kierunków, sal lub utrudnień.
builder.Services.AddScoped<IBiezacyUzytkownik, BiezacyUzytkownik>();
builder.Services.AddScoped<AudytInterceptor>();
builder.Services.AddScoped<UniewaznianieGrafuInterceptor>();
builder.Services.AddDbContext<PrzewodnikDbContext>((uslugi, opcje) => opcje
    .UseSqlServer(lancuchPolaczenia)
    .AddInterceptors(uslugi.GetRequiredService<AudytInterceptor>(), uslugi.GetRequiredService<UniewaznianieGrafuInterceptor>()));

// Konta: hasło min. 12 znaków z wielką i małą literą, cyfrą i znakiem specjalnym;
// blokada na 15 minut po 5 nieudanych próbach.
builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(o =>
    {
        o.Password.RequiredLength = 12;
        o.Password.RequireUppercase = true;
        o.Password.RequireLowercase = true;
        o.Password.RequireDigit = true;
        o.Password.RequireNonAlphanumeric = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.Lockout.AllowedForNewUsers = true;
        o.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<PrzewodnikDbContext>()
    .AddErrorDescriber<PolskieBledyTozsamosci>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/konto/logowanie";
    o.LogoutPath = "/konto/wylogowanie";
    o.AccessDeniedPath = "/konto/brak-dostepu";
    o.Cookie.Name = "przewodnik_sesja";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    o.SlidingExpiration = true;
});

builder.Services.AddScoped<ISpacerRepozytorium, SpacerRepozytorium>();
builder.Services.AddScoped<NawigacjaService>();

builder.Services.AddScoped<IWyszukiwarkaRepozytorium, WyszukiwarkaRepozytorium>();
builder.Services.AddScoped<WyszukiwarkaSalService>();

// Graf budynku jest buforowany w pamięci; panel administratora unieważni go po każdej zmianie danych.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IGrafBudynkuCache, GrafBudynkuCache>();
builder.Services.AddScoped<ITrasaRepozytorium, TrasaRepozytorium>();
builder.Services.AddSingleton<GeneratorOpisuService>();
builder.Services.AddScoped<WyszukiwarkaTrasService>();
builder.Services.Configure<KontaktAlternatywnyOptions>(builder.Configuration.GetSection(KontaktAlternatywnyOptions.Sekcja));

// Ustawienia dostępności są w ciasteczku — działają bez konta i bez JavaScriptu.
builder.Services.AddScoped<UstawieniaDostepnosciService>();

builder.Services.Configure<DeklaracjaDostepnosciOptions>(builder.Configuration.GetSection(DeklaracjaDostepnosciOptions.Sekcja));
builder.Services.AddScoped<IZgloszeniaRepozytorium, ZgloszeniaRepozytorium>();
builder.Services.AddScoped<ZgloszeniaService>();

// Panel administratora.
builder.Services.AddScoped<IAdministracjaRepozytorium, AdministracjaRepozytorium>();
builder.Services.AddScoped<AdministracjaService>();
builder.Services.AddScoped<WalidatorGrafuService>();
builder.Services.Configure<ZdjeciaOptions>(builder.Configuration.GetSection(ZdjeciaOptions.Sekcja));
builder.Services.AddSingleton<IMagazynZdjec, MagazynZdjec>();
builder.Services.AddScoped<ZdjeciaService>();

// Dane budynków w pliku w repozytorium: zapis z panelu, odczyt przy starcie na pustej bazie.
builder.Services.Configure<DaneBudynkowOptions>(builder.Configuration.GetSection(DaneBudynkowOptions.Sekcja));
builder.Services.AddScoped<DaneBudynkowService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/blad/500");
    app.UseHsts();
}

// Nagłówki bezpieczeństwa na każdej odpowiedzi. Serwis nie ma skryptów ani stylów w treści stron,
// więc polityka dopuszcza wyłącznie pliki z własnego adresu (obrazki także z data: — ikony Bootstrapa).
var politykaTresci = "default-src 'self'; img-src 'self' data:; object-src 'none'; base-uri 'self'; "
    + "form-action 'self'; frame-ancestors 'none'"
    // Odświeżanie strony przez „dotnet watch” łączy się gniazdem z innym portem.
    + (app.Environment.IsDevelopment() ? "; connect-src 'self' ws: wss:" : "");
app.Use(async (kontekst, dalej) =>
{
    var naglowki = kontekst.Response.Headers;
    naglowki.ContentSecurityPolicy = politykaTresci;
    naglowki.XContentTypeOptions = "nosniff";
    naglowki["Referrer-Policy"] = "no-referrer";
    naglowki.XFrameOptions = "DENY";
    await dalej();
});

app.UseStatusCodePagesWithReExecute("/blad/{0}");

app.UseHttpsRedirection();

var kultura = new CultureInfo("pl-PL");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(kultura),
    SupportedCultures = [kultura],
    SupportedUICultures = [kultura],
});

// Zdjęcia w wwwroot/media są wgrywane w czasie działania aplikacji, więc nie ma ich
// w manifeście MapStaticAssets — obsługuje je UseStaticFiles.
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// Przy każdym starcie: role oraz — tylko gdy w bazie nie ma jeszcze żadnego budynku — dane z pliku
// w repozytorium. Konto deweloperskie wyłącznie w środowisku deweloperskim. Gdy baza nie istnieje,
// aplikacja i tak startuje: strony bez zapytań do bazy działają, a w logu jest wskazówka.
using (var zakres = app.Services.CreateScope())
{
    try
    {
        await TozsamoscSeeder.ZasiejRoleAsync(zakres.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>());

        var dane = zakres.ServiceProvider.GetRequiredService<DaneBudynkowService>();
        if (await dane.WczytajDoPustejBazyAsync() is { } wczytane)
        {
            app.Logger.LogInformation(
                "Wczytano dane z pliku {Plik}: budynki {Budynki}, punkty ruchu {Punkty}, kierunki {Kierunki}, sale {Sale}, zdjęcia {Zdjecia}.",
                dane.SciezkaPliku, wczytane.Budynki, wczytane.Punkty, wczytane.Kierunki, wczytane.Sale, wczytane.Zdjecia);
        }

        if (app.Environment.IsDevelopment())
        {
            await TozsamoscSeeder.ZasiejAdministratoraDevAsync(
                zakres.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>(), app.Configuration, app.Logger);
        }
    }
    // Brak bazy albo tabel: EF zgłasza SqlException bezpośrednio albo opakowany. Błąd w samych danych
    // (DbUpdateException) nie jest tu łapany — ma zatrzymać start.
    catch (Exception blad) when (blad is SqlException || blad.InnerException is SqlException)
    {
        app.Logger.LogError(blad,
            "Nie udało się przygotować ról i danych początkowych. Sprawdź, czy baza jest aktualna: „dotnet ef database update”.");
    }
}

app.Run();

// Potrzebne testom: WebApplicationFactory<Program> wymaga publicznego typu.
public partial class Program;
