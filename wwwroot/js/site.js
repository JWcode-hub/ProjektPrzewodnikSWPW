// Multimedialny przewodnik po Uczelni — skrypty usprawniające.
// Każda funkcja serwisu działa także bez tego pliku.
//
// ZASADA OGŁASZANIA — jedna dla całego serwisu:
//
// 1. PEŁNE PRZEŁADOWANIE strony → obszar aria-live zostaje PUSTY. Wynik akcji niosą:
//    - <title>, który czytnik ekranu odczytuje jako pierwszy (działa bez JavaScriptu),
//    - element, na który trafia fokus (niżej).
//    Ten sam tekst wpisany dodatkowo do aria-live byłby czytany drugi raz.
//
// 2. ZMIANA BEZ PRZEŁADOWANIA → przewodnik.oglos(). Tylko wtedy.
//
// Jedyny obszar aria-live strony to #komunikaty w _Layout.cshtml.
(function () {
  'use strict';

  function oglos(tekst, pilne) {
    var obszar = document.getElementById('komunikaty');
    if (!obszar) { return; }
    obszar.setAttribute('aria-live', pilne ? 'assertive' : 'polite');
    obszar.textContent = '';                                      // wymusza ponowne ogłoszenie
    setTimeout(function () { obszar.textContent = tekst; }, 50);  // tego samego tekstu
  }

  window.przewodnik = { oglos: oglos };

  // Fokus po załadowaniu — JEDYNE miejsce w serwisie, które go ustawia.
  // Pierwszeństwo ma element z autofocus (pierwsze błędne pole formularza); gdy go nie ma,
  // fokus dostaje pierwszy element oznaczony w widoku atrybutem data-fokus-po-zaladowaniu.
  var cel = document.querySelector('[autofocus]') || document.querySelector('[data-fokus-po-zaladowaniu]');
  if (cel) {
    cel.focus();
  }
})();
