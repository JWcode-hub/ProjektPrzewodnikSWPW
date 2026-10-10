// Ekran miejsca — wyłącznie usprawnienia. Bez tego pliku spacer działa w pełni: przejścia to zwykłe linki.
// Fokus na nazwie miejsca ustawia site.js (atrybut data-fokus-po-zaladowaniu w widoku).
(function () {
  'use strict';

  // Skróty zawsze z klawiszem Alt, nigdy gołe litery ani strzałki — tych używa czytnik ekranu.
  // Kolejność cyfr jak na liście przejść. Alt+S i Alt+T celowo nieprzypisane (kolizja z menu Firefoksa).
  var SKROTY = {
    Digit1: 'prosto', Numpad1: 'prosto',
    Digit2: 'lewo', Numpad2: 'lewo',
    Digit3: 'prawo', Numpad3: 'prawo',
    Digit4: 'tyl', Numpad4: 'tyl'
  };

  document.addEventListener('keydown', function (e) {
    // Wyłącznie sam Alt. Ctrl+Alt to AltGr w polskim układzie klawiatury; Shift i Meta też pomijamy.
    if (!e.altKey || e.ctrlKey || e.metaKey || e.shiftKey) { return; }
    // Użytkownik może wyłączyć skróty w ustawieniach (WCAG 2.1.4).
    if (document.documentElement.dataset.skroty === 'wylaczone') { return; }

    // e.code nie zależy od układu klawiatury (na macOS Option+2 daje e.key „™”, ale e.code „Digit2”).
    // Część klawiatur ekranowych wysyła zdarzenia bez e.code — wtedy e.key.
    if (e.code === 'KeyP' || (!e.code && (e.key === 'p' || e.key === 'P'))) {
      // Alt+P — powtórz opis miejsca. Przy włączonej mowie czyta głos przeglądarki (mowa.js) i nic nie
      // trafia do aria-live, żeby nie mówiły dwa głosy naraz. Inaczej opis ogłasza czytnik ekranu.
      // W obu przypadkach ten sam tekst: opis głosowy miejsca i notatki o dojściu.
      var opis = document.getElementById('opis-miejsca');
      if (window.przewodnik && window.przewodnik.mowa) {
        e.preventDefault();
        window.przewodnik.mowa.czytaj();
      } else if (window.przewodnik && opis) {
        e.preventDefault();
        window.przewodnik.oglos((opis.dataset.doOgloszenia || opis.textContent).trim(), true);
      }
      return;
    }

    var kierunek = SKROTY[e.code] || SKROTY['Digit' + e.key];
    if (!kierunek) { return; }

    var pozycja = document.querySelector('.lista-kierunkow [data-kierunek="' + kierunek + '"]');
    if (!pozycja) { return; }
    e.preventDefault();

    var link = pozycja.querySelector('a');
    if (link) {
      link.click();
    } else if (window.przewodnik) {
      // Brak przejścia: nie przeładowujemy strony, tylko ogłaszamy opis przeszkody.
      window.przewodnik.oglos(pozycja.textContent.replace(/\s+/g, ' ').trim(), true);
    }
  });
})();
