# Napisy AI - wtyczka Emby

Status: zatwierdzony (2026-10-24)

## Cel

Wtyczka serwera Emby, która tłumaczy angielskie napisy na wybrany język (domyślnie polski) przez AI. Instaluje się ją jako jeden plik `.dll` w folderze wtyczek Emby. Nie ma osobnego programu ani osobnej strony.

## Jak działa

- **Ustawienia wtyczki w panelu Emby** (Wtyczki → Napisy AI): dostawca AI (Gemini, Claude, API zgodne z OpenAI), klucz API, model, adres API (dla OpenAI-zgodnych), język docelowy, limit zapytań na minutę, przełącznik automatycznego tłumaczenia.
- **„Szukaj napisów”** przy filmie lub odcinku: gdy szukany język to język docelowy, a element ma angielskie napisy (zewnętrzne albo wbudowane tekstowe), w wynikach jest pozycja „Tłumaczenie AI (<język>)”. Po wybraniu wtyczka tłumaczy napisy i oddaje je Emby jako SRT; Emby zapisuje je tak jak napisy z każdego innego źródła.
- **Zaplanowane zadanie** „Napisy AI: przetłumacz brakujące napisy”: domyślnie bez wyzwalacza; tłumaczy filmy i odcinki, które mają angielskie napisy, a nie mają napisów w języku docelowym. Zapisuje wynik obok pliku wideo jako `<nazwa>.<kod języka>.srt` i odświeża element.

## Zasady (bez zmian względem wcześniejszych ustaleń)

- Źródło: napisy znane Emby (`MediaStream`): najpierw zewnętrzne angielskie pełne, potem SDH, potem wbudowane tekstowe angielskie, na końcu `forced`. Napisy obrazkowe (PGS/VobSub) są pomijane.
- Element pomijany, jeśli ma już napisy w języku docelowym (zewnętrzne lub wbudowane).
- Tłumaczenie w paczkach (domyślnie 60 kwestii) z kontekstem kilkunastu poprzednich kwestii i tytułem; odpowiedź JSON z tymi samymi `id`; walidacja; ponowienie, potem podział paczki, potem błąd. Czasy i liczba kwestii 1:1.
- Nigdy nie nadpisuje istniejącego pliku napisów (zadanie zaplanowane zapisuje tylko, gdy plik docelowy nie istnieje). Zapis przez plik tymczasowy i zamianę.
- Pliki wideo tylko do odczytu. Do dostawcy AI trafia tylko tekst napisów i tytuł.
- Limity: zapytania po kolei, konfigurowalny limit na minutę, ponawianie przy 429 z `Retry-After`. Wyczerpany limit dzienny przerywa bieżące tłumaczenie z czytelnym komunikatem; zadanie zaplanowane kończy przebieg i spróbuje przy następnym uruchomieniu.
- Kodowanie plików wejściowych wykrywane (UTF-8, Windows-1250, ISO-8859-2, Windows-1252); wynik UTF-8.

## Ryzyko

Tłumaczenie odcinka na darmowym Gemini trwa 1-2 minuty. Trzeba sprawdzić, czy „Szukaj napisów” → pobranie czeka tak długo. Jeśli nie, pobranie uruchamia tłumaczenie w tle, a napisy pojawiają się po zakończeniu (zapis obok wideo + odświeżenie elementu).

## Technicznie

- C#, `netstandard2.0`, pakiet NuGet `MediaBrowser.Server.Core` 4.9.x (Emby 4.9). Bez dodatkowych zależności (HTTP przez `System.Net.Http`, JSON przez API Emby `IJsonSerializer` lub ręcznie).
- Strona ustawień: osadzony HTML + JS (klasyczna strona konfiguracji wtyczki Emby).
- Napisy wbudowane wyciągane przez ffmpeg dostarczany z Emby (`IMediaEncoder.EncoderPath`).
- Testy: xUnit dla logiki niezależnej od Emby (SRT, kodowanie, paczki, prompt, walidacja, dostawcy z atrapą HTTP, wybór źródła).

## Poza zakresem

Plex, osobny panel/serwer, logowanie subskrypcją, OCR napisów obrazkowych, języki źródłowe inne niż angielski.
