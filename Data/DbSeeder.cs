using Microsoft.EntityFrameworkCore;
using PrzewodnikSWPW.Web.Models;
using PrzewodnikSWPW.Web.Services;

namespace PrzewodnikSWPW.Web.Data;

/// <summary>
/// Dane początkowe: zachodnia część parteru budynku A. Słowniki (typy sal, typy punktów,
/// udogodnienia) są już w bazie — wstawia je migracja.
///
/// Zasady pisania opisów:
/// - opis punktu nie mówi „po lewej” ani „po prawej” względem idącego, bo do punktu wchodzi się
///   z kilku stron; strona względem nazwanego obiektu jest w porządku („po prawej stronie drzwi windy”),
/// - opis przejścia mówi, co się mija po drodze; nazwę celu, stronę i odległość dokleja aplikacja,
/// - opis głosowy to krótkie zdania bez skrótów, z liczbami zapisanymi słownie.
/// </summary>
public static class DbSeeder
{
    public const string KodBudynku = "A";

    /// <summary>Wstawia dane, jeśli budynku A jeszcze nie ma. Zwraca <c>true</c>, gdy coś wstawiono.</summary>
    public static async Task<bool> ZasiejAsync(PrzewodnikDbContext db, CancellationToken ct = default)
    {
        if (await db.Budynki.AnyAsync(b => b.Kod == KodBudynku, ct))
        {
            return false;
        }

        await using var transakcja = await db.Database.BeginTransactionAsync(ct);

        var typySal = await db.TypySal.ToDictionaryAsync(t => t.Nazwa, ct);
        var typyPunktow = await db.TypyPunktow.ToDictionaryAsync(t => t.Nazwa, ct);
        var udogodnienia = await db.Udogodnienia.ToDictionaryAsync(u => u.Nazwa, ct);

        var budynek = new Budynek
        {
            Kod = KodBudynku,
            Nazwa = "Budynek A — Szkoła Wyższa im. Pawła Włodkowica",
            Adres = "al. Kilińskiego 12, 09-402 Płock",
            CzyMaWinde = true,
            OpisDostepnosciArchitektonicznej =
                "Wejście główne od strony ulicy, bez progu, drzwi dwuskrzydłowe otwierane ręcznie. " +
                "Korytarze o szerokości powyżej 150 cm. Winda dostępna z holu głównego, " +
                "z sygnalizacją głosową i oznaczeniami brajlowskimi. Toaleta dostosowana na parterze. " +
                "Do budynku można wejść z psem asystującym. Miejsca parkingowe dla osób " +
                "z niepełnosprawnością wyznaczone przed wejściem głównym.",
        };
        var parter = new Pietro { Budynek = budynek, Numer = 0, Nazwa = "Parter", Opis = "Parter budynku A." };

        // --- Punkty ruchu -------------------------------------------------------------------------
        var punkty = new Dictionary<string, PunktRuchu>();

        void DodajPunkt(string kod, string typ, string nazwa, int azymut, decimal x, decimal y, string opis, string opisGlosowy) =>
            punkty[kod] = new PunktRuchu
            {
                Pietro = parter,
                TypPunktu = typyPunktow[typ],
                Kod = $"A-0-{kod}",
                Nazwa = nazwa,
                Opis = opis,
                OpisGlosowy = opisGlosowy,
                AzymutDomyslny = azymut,
                X = x,
                Y = y,
            };

        DodajPunkt("P01", "Hol", "Wejście główne", 0, 0, 0,
            "Wejście główne do budynku A od strony ulicy. Drzwi dwuskrzydłowe, bez progu. " +
            "Od drzwi zaczyna się ścieżka dotykowa w podłodze, która prowadzi do windy.",
            "Jesteś przy wejściu głównym do budynku A. Drzwi wejściowe mają dwa skrzydła i nie mają progu. " +
            "Za drzwiami zaczyna się hol. W podłodze jest ścieżka dotykowa, która prowadzi do windy.");

        DodajPunkt("P02", "Hol", "Hol główny przy portierni", 0, 0, 6,
            "Duży hol. Okienko portierni i tablica informacyjna są po przeciwnych stronach holu. " +
            "Przez hol biegnie ścieżka dotykowa.",
            "Jesteś w holu głównym. Około trzech metrów stąd jest okienko portierni. " +
            "Naprzeciwko okienka, po drugiej stronie holu, wisi tablica informacyjna. " +
            "Przez hol biegnie ścieżka dotykowa.");

        DodajPunkt("P03", "Skrzyżowanie", "Skrzyżowanie korytarzy", 0, 0, 14,
            "Skrzyżowanie korytarza zachodniego i wschodniego. W podłodze ścieżka dotykowa. " +
            "Stąd dochodzi się do sal komputerowych, sekretariatu, windy i holu głównego.",
            "Jesteś na skrzyżowaniu korytarzy. Zbiegają się tu cztery drogi. " +
            "Korytarz zachodni prowadzi do sal komputerowych. Korytarz wschodni prowadzi do sekretariatu. " +
            "Są tu też przejście do windy i przejście do holu głównego.");

        DodajPunkt("P04", "Korytarz", "Korytarz zachodni przy sali A12", 270, -5, 14,
            "Odcinek korytarza zachodniego. Są tu drzwi do sali A12, a naprzeciwko nich ściana z gablotą informacyjną.",
            "Jesteś w korytarzu zachodnim. Są tu drzwi do sali A dwanaście. " +
            "Naprzeciwko tych drzwi jest ściana z gablotą informacyjną.");

        DodajPunkt("P05", "Korytarz", "Korytarz zachodni przy salach A14 i A15", 270, -11, 14,
            "Odcinek korytarza zachodniego. Są tu drzwi do sali A14 i do sali A15, naprzeciwko siebie.",
            "Jesteś w korytarzu zachodnim. Są tu drzwi do sali A czternaście i do sali A piętnaście. " +
            "Drzwi obu sal są naprzeciwko siebie.");

        DodajPunkt("P06", "Korytarz", "Koniec korytarza zachodniego", 270, -17, 14,
            "Koniec korytarza zachodniego. Są tu schody na pierwsze piętro, a naprzeciwko nich drzwi toalety dostosowanej.",
            "Jesteś na końcu korytarza zachodniego. Są tu schody na pierwsze piętro. " +
            "Naprzeciwko schodów są drzwi toalety dostosowanej dla osób z niepełnosprawnością.");

        DodajPunkt("P07", "Podest schodów", "Podest schodów zachodnich", 0, -17, 17,
            "Spocznik przed biegiem schodów na pierwsze piętro. Poręcze po obu stronach schodów. " +
            "Pierwszy i ostatni stopień mają oznaczenie kontrastowe.",
            "Jesteś na podeście schodów zachodnich. Schody prowadzą w górę na pierwsze piętro. " +
            "Mają osiemnaście stopni. Poręcze są po obu stronach schodów.");

        DodajPunkt("P08", "Przed windą", "Przed windą", 0, 0, 18,
            "Miejsce przed drzwiami windy. Przycisk przywołania jest po prawej stronie drzwi windy i ma oznaczenie brajlowskie. " +
            "Kończy się tu ścieżka dotykowa.",
            "Jesteś przed windą. Przycisk przywołania jest po prawej stronie drzwi windy, na wysokości ręki. " +
            "Przycisk jest oznaczony pismem Braille'a. Winda zapowiada piętra głosem.");

        DodajPunkt("P09", "Korytarz", "Korytarz wschodni przy sekretariacie", 90, 7, 14,
            "Odcinek korytarza wschodniego. Są tu drzwi sekretariatu Wydziału Informatyki, sala A16.",
            "Jesteś w korytarzu wschodnim. Są tu drzwi sekretariatu Wydziału Informatyki. To sala A szesnaście.");

        // --- Sale ---------------------------------------------------------------------------------
        var sale = new Dictionary<string, Sala>();

        void DodajSale(string symbol, string typ, string punktWejsciowy, string nazwa, string aliasy,
                  int? liczbaMiejsc, string opis, string opisGlosowy) =>
            sale[symbol] = new Sala
            {
                Pietro = parter,
                TypSali = typySal[typ],
                PunktWejsciowy = punkty[punktWejsciowy],
                Symbol = symbol,
                Nazwa = nazwa,
                Aliasy = aliasy,
                Opis = opis,
                OpisGlosowy = opisGlosowy,
                LiczbaMiejsc = liczbaMiejsc,
                CzyDostepnaDlaWozkow = true,
            };

        DodajSale("A12", "Sala komputerowa", "P04", "Pracownia komputerowa", "12;sala 12;pracownia", 16,
            "Sala z 16 stanowiskami komputerowymi. Wejście bez progu.",
            "Sala A dwanaście, pracownia komputerowa. Ma szesnaście stanowisk. Wejście jest bez progu.");
        DodajSale("A14", "Sala ćwiczeniowa", "P05", "Sala ćwiczeniowa", "14;sala 14", 30,
            "Sala ćwiczeniowa na 30 osób.",
            "Sala A czternaście, sala ćwiczeniowa na trzydzieści osób.");
        DodajSale("A15", "Sala komputerowa", "P05", "Pracownia sieci komputerowych", "15;sala 15;sieci", 12,
            "Pracownia sieciowa z 12 stanowiskami i szafą z urządzeniami sieciowymi.",
            "Sala A piętnaście, pracownia sieci komputerowych. Ma dwanaście stanowisk.");
        DodajSale("A16", "Sekretariat", "P09", "Sekretariat Wydziału Informatyki", "dziekanat;sekretariat;16", null,
            "Sekretariat czynny od poniedziałku do piątku w godzinach 8:00–15:00. Przy okienku działa pętla indukcyjna.",
            "Sala A szesnaście, sekretariat Wydziału Informatyki. Czynny od poniedziałku do piątku, od ósmej do piętnastej. " +
            "Przy okienku działa pętla indukcyjna.");
        DodajSale("WC-0-Z", "Toaleta", "P06", "Toaleta dostosowana", "wc;toaleta;łazienka", null,
            "Toaleta dla osób z niepełnosprawnością, z poręczami i przyciskiem alarmowym.",
            "Toaleta dostosowana. Drzwi otwierają się na zewnątrz. W środku są poręcze i przycisk alarmowy przy umywalce.");

        // --- Kierunki -----------------------------------------------------------------------------
        // Azymuty budynku A: 0 = w głąb budynku, 90 = w prawo od wejścia, 180 = w stronę wejścia,
        // 270 = w lewo od wejścia. To strony BUDYNKU — „w lewo” dla użytkownika wylicza aplikacja.
        var pary = new List<(Kierunek Tam, Kierunek ZPowrotem)>();
        var kierunki = new List<Kierunek>();

        // Przejście między punktami zawsze powstaje parą: tam i z powrotem, azymut przeciwny, ta sama waga.
        void Przejscie(string z, string @do, int azymut, decimal metry, string opisTam, string opisZPowrotem,
                       RodzajPrzejscia rodzaj = RodzajPrzejscia.Korytarz)
        {
            Kierunek Nowy(string a, string b, int az, string opis) => new()
            {
                PunktZrodlowy = punkty[a],
                PunktDocelowy = punkty[b],
                Azymut = az,
                Waga = metry,
                RodzajPrzejscia = rodzaj,
                OpisPrzejscia = opis,
                CzyDostepnyBezSchodow = rodzaj != RodzajPrzejscia.Schody,
            };

            var para = (Nowy(z, @do, azymut, opisTam), Nowy(@do, z, Azymuty.Normalizuj(azymut + 180), opisZPowrotem));
            pary.Add(para);
            kierunki.Add(para.Item1);
            kierunki.Add(para.Item2);
        }

        // Drzwi do sali — bez kierunku powrotnego: z sali wraca się do jej punktu wejściowego.
        void Drzwi(string z, string symbolSali, int azymut, decimal metry, string opis) =>
            kierunki.Add(new Kierunek
            {
                PunktZrodlowy = punkty[z],
                SalaDocelowa = sale[symbolSali],
                Azymut = azymut,
                Waga = metry,
                RodzajPrzejscia = RodzajPrzejscia.Drzwi,
                OpisPrzejscia = opis,
            });

        // Brak przejścia opisany jako punkt orientacyjny. Baza wymaga, żeby każdy kierunek miał cel,
        // więc formalnym celem jest sala po przeciwnej stronie korytarza; aplikacja go nie pokazuje.
        void Przeszkoda(string z, string formalnyCel, int azymut, string opis) =>
            kierunki.Add(new Kierunek
            {
                PunktZrodlowy = punkty[z],
                SalaDocelowa = sale[formalnyCel],
                Azymut = azymut,
                Waga = 0.5m,
                RodzajPrzejscia = RodzajPrzejscia.Drzwi,
                OpisPrzejscia = opis,
                CzyAktywny = false,
            });

        Przejscie("P01", "P02", 0, 6,
            "Przez drzwi wejściowe bez progu, dalej wzdłuż ścieżki dotykowej.",
            "Wzdłuż ścieżki dotykowej do drzwi wejściowych. Za drzwiami jest ulica.");
        Przejscie("P02", "P03", 0, 8,
            "Przez hol, wzdłuż ścieżki dotykowej.",
            "Wzdłuż ścieżki dotykowej w stronę wejścia.");
        Przejscie("P03", "P08", 0, 4,
            "Wzdłuż ścieżki dotykowej, która kończy się przed drzwiami windy.",
            "Wzdłuż ścieżki dotykowej, od windy w stronę wejścia.");
        Przejscie("P03", "P04", 270, 5,
            "Korytarzem zachodnim. Pierwsze drzwi po drodze to sala A12.",
            "Korytarzem zachodnim. Korytarz kończy się na skrzyżowaniu, gdzie zaczyna się ścieżka dotykowa.");
        Przejscie("P04", "P05", 270, 6,
            "Korytarzem zachodnim, dalej od skrzyżowania.",
            "Korytarzem zachodnim, w stronę skrzyżowania.");
        Przejscie("P05", "P06", 270, 6,
            "Korytarzem zachodnim do jego końca.",
            "Korytarzem zachodnim, w stronę skrzyżowania.");
        Przejscie("P03", "P09", 90, 7,
            "Korytarzem wschodnim.",
            "Korytarzem wschodnim. Korytarz kończy się na skrzyżowaniu, gdzie zaczyna się ścieżka dotykowa.");
        Przejscie("P06", "P07", 0, 3,
            "Do podestu, od którego zaczynają się schody w górę. Pierwszy stopień ma oznaczenie kontrastowe.",
            "Z podestu schodów do korytarza zachodniego.",
            RodzajPrzejscia.Schody);

        Drzwi("P04", "A12", 0, 1, "Drzwi bez progu.");
        Drzwi("P05", "A14", 0, 1, "Drzwi sali ćwiczeniowej.");
        Drzwi("P05", "A15", 180, 1, "Drzwi pracowni sieci komputerowych.");
        Drzwi("P09", "A16", 0, 1, "Drzwi sekretariatu. Okienko z pętlą indukcyjną jest w środku.");
        Drzwi("P06", "WC-0-Z", 180, 2, "Drzwi toalety otwierają się na zewnątrz, w stronę korytarza.");

        Przeszkoda("P04", "A12", 180, "Ściana z gablotą informacyjną.");

        // --- Udogodnienia -------------------------------------------------------------------------
        void DodajUdogodnienie(string kodPunktu, string nazwa, string uwagi) =>
            db.PunktUdogodnienia.Add(new PunktUdogodnienie
            {
                PunktRuchu = punkty[kodPunktu],
                Udogodnienie = udogodnienia[nazwa],
                Uwagi = uwagi,
            });

        DodajUdogodnienie("P08", "Winda", "Winda z zapowiedzią głosową pięter");
        DodajUdogodnienie("P08", "Oznaczenia brajlowskie", "Przyciski oznaczone brajlem");
        DodajUdogodnienie("P03", "Ścieżka dotykowa", "Ścieżka dotykowa prowadzi od wejścia do windy");
        DodajUdogodnienie("P07", "Oznaczenie kontrastowe", "Kontrastowe oznaczenie pierwszego i ostatniego stopnia");
        db.SalaUdogodnienia.Add(new SalaUdogodnienie
        {
            Sala = sale["A16"],
            Udogodnienie = udogodnienia["Pętla indukcyjna"],
            Uwagi = "Pętla indukcyjna przy okienku",
        });

        // --- Zapis w dwóch krokach ----------------------------------------------------------------
        // Krok 1: wszystkie rekordy dostają Id.
        db.Budynki.Add(budynek);
        db.PunktyRuchu.AddRange(punkty.Values);
        db.Sale.AddRange(sale.Values);
        db.Kierunki.AddRange(kierunki);
        await db.SaveChangesAsync(ct);

        // Krok 2: wskazania, które wymagają istniejących Id. Budynek i punkt wejścia wskazują na siebie
        // (budynek → piętro → punkt, punkt ← budynek), tak samo kierunek i jego powrót — takich par
        // nie da się wstawić jednym INSERT-em.
        budynek.PunktWejsciaGlownego = punkty["P01"];
        foreach (var (tam, zPowrotem) in pary)
        {
            tam.KierunekPowrotny = zPowrotem;
            zPowrotem.KierunekPowrotny = tam;
        }
        await db.SaveChangesAsync(ct);

        await transakcja.CommitAsync(ct);
        return true;
    }
}
