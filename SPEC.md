# SPEC – radioMAT (2026-11-05)

## Cel i miara sukcesu

Przygotować pierwsze wydanie radioMAT w dwóch odmianach: wewnętrznej i publicznej
MATCODE. Obie nazywają się radioMAT. Najpierw przygotowujemy i wydajemy odmianę
wewnętrzną. Dotychczasowy instalator 0.2.0 nie został przekazany odbiorcom wewnętrznym.
Zakres zaakceptowany poleceniem Mateusza „działamy zgodnie z tą specyfikacją” po
odczytaniu wiadomości 20261105-1412-matcode. Szczegóły wspólne określa lokalna
centrala wydań; jej danych wewnętrznych nie kopiujemy do publicznego kodu.

Sukces: dwa rozdzielone instalatory dla Windows 11 x64, działające radio,
aktualizacje sprawdzające podpis właściwego wydania, pozytywny odbiór Mateusza
i publikacja dopiero po osobnym upoważnieniu w bieżącej sesji.

## Kto używa

- Mateusz i odbiorcy wewnętrzni: wydanie wewnętrzne po polsku.
- Użytkownicy publiczni: wydanie MATCODE po polsku lub angielsku.
- Instalacja dla bieżącego konta Windows, bez administratora, z dołączonym .NET.

## Historie i warunki odbioru

### H1. Rozdzielone wydania

Jako osoba przygotowująca program chcę wybierać wydanie, żeby każdy instalator
zawierał tylko właściwą tożsamość i ustawienia.

- K1.1 Gdy buduję instalator, to jawnie wybieram wydanie; oba mają jedno źródło wersji.
- K1.2 Gdy brakuje wymaganych lokalnych danych wydania wewnętrznego lub są błędne,
  to budowanie zatrzymuje się przed kompilacją; nie podstawia danych publicznych.
- K1.3 Gdy powstaje paczka wewnętrzna, to nie zawiera marki MATCODE, jej linków,
  tekstów, grafik ani publicznego klucza drugiego wydania. Kontrola paczki i
  instrukcji instalatora daje CZYSTO przed utworzeniem instalatora.
- K1.4 Gdy powstaje paczka i migawka publiczna, to nie zawiera danych organizacji,
  adresów wewnętrznych ani identyfikatorów drugiego wydania; kontrola daje CZYSTO.
- K1.5 Gdy buduję oba wydania, to mają odrębne AppId, tożsamość systemową,
  ustawienia, autostart i blokadę pojedynczej instancji. Dane publicznej wersji
  roboczej 0.2.0 pozostają dostępne w jej następcy publicznym.
- K1.6 Gdy przygotowuję wydanie, to każde ma własny opis „Co nowego”: wewnętrzny
  (po polsku) bez wzmianki o wydaniu publicznym, wsparciu autora, kawie i „dwóch
  wydaniach”; publiczny bez wzmianki o wydaniu wewnętrznym. Opis z niedozwolonym
  słowem zatrzymuje build przed instalatorem (Mateusz 11.11, od 0.3.1).

### H2. Instalacja i dotychczasowe radio

Jako słuchacz chcę instalować i aktualizować program bez utraty stacji.

- K2.1 Gdy instaluję program po raz pierwszy, to działa na Windows 11 x64 bez
  osobnej instalacji .NET i bez administratora; sam build nie uruchamia programu.
- K2.2 Gdy aktualizuję tę samą odmianę, to zostają moje trzy stacje, głośność
  i autostart; druga odmiana nie jest nadpisywana.
- K2.3 Gdy używam radia, to zachowane są MP3, katalog polskich stacji, trzy skróty,
  tytuły ICY, pojedyncza instancja oraz start i obsługa w zasobniku.
- K2.4 Gdy profil lub ścieżka ma spacje i polskie znaki, to instalacja i aktualizacja
  działają. Odinstalowanie usuwa własne skróty i autostart, zachowując ustawienia.
- K2.5 Gdy aktualizuję program, to instalator przed skopiowaniem nowej wersji usuwa
  stare pliki programu z folderu instalacji, więc nie zostają resztki starszych wersji;
  ustawienia (AppData) i autostart zostają. Sprzątanie jest ciche, bez wzmianki
  w „Co nowego” (zasada centrali MATCODE 11.11, od 0.3.1).

### H3. Podpisane aktualizacje

Jako słuchacz chcę bezpiecznie otrzymywać aktualizacje właściwego wydania.

- K3.1 Gdy program startuje lub wybieram „Sprawdź aktualizacje”, to sprawdza
  właściwe źródło: lokalnie skonfigurowany folder wewnętrzny albo GitHub Releases.
- K3.2 Gdy dostępna jest nowsza wersja, to widzę „Co nowego”, „Zaktualizuj teraz”,
  „Pomiń tę wersję” i „Później”; pominięcie dotyczy wskazanej wersji.
- K3.3 Gdy instalator nie ma poprawnego podpisu właściwym kluczem lub jego suma
  nie pasuje, to program go nie uruchamia i pokazuje zrozumiały komunikat.
  Każda odmiana ma osobny klucz Ed25519.
- K3.4 Gdy brakuje internetu, folderu, uprawnień lub poprawnego opisu wersji,
  to odtwarzacz działa dalej i podaje spokojny komunikat; nie ma cichej awarii.
- K3.5 Gdy potwierdzam aktualizację, to instalator działa bez dodatkowego kreatora,
  a program wraca po zakończeniu. Podwójne kliknięcie nie uruchamia dwóch instalacji.
- K3.6 Gdy opis lub nazwa pliku w opisie aktualizacji są błędne, to nie uruchamia się
  plik spoza właściwego źródła; tekst np. `<b>x</b>` jest zwykłym tekstem.

### H4. Informacje i kontakt

Jako użytkownik chcę znaleźć wersję, licencje i właściwą pomoc.

- K4.1 Gdy otwieram „O programie”, to widzę nazwę, wersję, jedno zdanie i licencje;
  w odmianie wewnętrznej dane opiekuna z pliku lokalnego, w publicznej MATCODE
  i stronę programu. Stopka zawiera właściwy podpis wersji.
- K4.2 Gdy używam publicznej odmiany, to w prawym górnym rogu mam cichą filiżankę
  z podpowiedzią „Postaw kawę autorowi”, prowadzącą do ustalonej strony wsparcia.
- K4.3 Gdy wybieram „Zgłoś problem”, to odmiana publiczna otwiera zgłoszenie w swoim
  repozytorium, a wewnętrzna kontakt mailowy z lokalnej konfiguracji. Odpowiednie
  pozycje są też w menu zasobnika. Prywatne ustawienia nie są automatycznie wysyłane.

### H5. Języki

Jako użytkownik publiczny chcę używać radia po polsku lub angielsku.

- K5.1 Gdy zmieniam PL/EN, to wybór zostaje zapisany i obowiązuje po restarcie;
  przetłumaczone są okna, komunikaty, menu zasobnika i instalator publiczny.
- K5.2 Gdy korzystam z katalogu lub ICY, to nazwy własne stacji i tekst nadawcy
  pozostają oryginalne. Długie teksty nie zasłaniają kontrolek małego okna.

### H6. Pierwsza publikacja

Jako autor chcę przekazać sprawdzony program i jego stronę.

- K6.1 Gdy przygotowuję publikację wewnętrzną, to mam instalator, podpis, SHA256
  i opis najnowszej wersji; opis trafia do źródła na końcu, po pozostałych plikach.
- K6.2 Gdy publikuję odmianę MATCODE, to poprzedza ją wydanie wewnętrzne; dostępne
  są podpisany instalator w Releases, strona według wspólnej ramy, karta na stronie
  głównej i wpis w profilu. Sam commit/push kodu nie jest publikacją wydania.
- K6.3 Gdy kończę etap, to zapisuję wyniki i pozostałe braki. Wiadomość ze skrzynki
  zamykam dopiero po wykonaniu jej zakresu; tabelę centrali aktualizuję zgodnie z faktami.
- K6.4 Gdy opublikuję wydanie, to skrypt sam sprawdza to, co faktycznie leży w źródle
  aktualizacji (dysk wewnętrzny albo Releases): pliki identyczne z przygotowanymi, podpis,
  SHA256, wersja i opis bez wzmianki o drugim wydaniu, marka; publicznie także
  widoczność repo, budowę strony i brak linków do prywatnego repo. Na koniec wypisuje,
  co jeszcze trzeba zaktualizować (tabela centrali, STAN, link pobierania na stronie).
  Ten sam skrypt da się uruchomić osobno, bez publikowania (Mateusz 11.11).

### H7. Głośność z zasobnika — uzgodnione 11.11

Jako słuchacz chcę zmieniać głośność z menu ikony przy zegarku, bez otwierania
głównego okna radia. Mateusz wybrał suwak widoczny od razu przy wszystkich
opcjach menu, bez dodatkowego rozwijania.

- K7.1 Gdy otwieram menu zasobnika, to od razu obok dotychczasowych poleceń
  widzę osobną kolumnę „Głośność” z pionowym suwakiem: 100% u góry, 0% u dołu
  i czytelny bieżący procent. Nie muszę otwierać podmenu.
- K7.2 Gdy przeciągam suwak, to od razu zmienia się tylko głośność radia; menu
  pozostaje otwarte przez całą regulację, a główne okno nie jest pokazywane.
  Kliknięcie poza menu lub Escape zamyka menu.
- K7.3 Gdy zmieniam głośność w oknie albo w zasobniku, to oba suwaki i procenty
  pokazują tę samą wartość; po zamknięciu programu i ponownym starcie wartość zostaje.
- K7.4 Gdy radio jest zatrzymane, to mogę ustawić głośność bez uruchamiania dźwięku;
  wznowienie używa wybranej wartości. 0% wycisza, 100% to maksimum radia.
- K7.5 Gdy używam klawiatury, to mogę wejść do regulacji i zmieniać ją strzałkami;
  dotychczasowe polecenia menu pozostają dostępne. Etykiety respektują język PL/EN.
- K7.6 Gdy otwieram menu, to jego wygląd pasuje do głównego okna: jasne tło,
  granatowy tekst, niebieski suwak, zaokrąglenia, czytelne odstępy i podświetlenie
  opcji. Nie ma pustego białego pasa przy prawej krawędzi ani starego systemowego
  wyglądu suwaka (uwaga Mateusza 11.11 po pierwszym odbiorze).
  Menu jest kompaktowe; wszystkie dotychczasowe opcje są od razu widoczne,
  bez systemowych strzałek przewijania u góry i dołu (kolejna uwaga Mateusza 11.11).
  Gdy na ekranie jest mniej miejsca, panel dopasowuje się w całości do obszaru
  roboczego bieżącego monitora (poza paskiem zadań); nie wymaga przewijania.
- K7.7 Gdy otwieram menu, to zamiast „Włącz / zatrzymaj radio” widzę jedną
  pozycję „Wycisz radio” z polem zaznaczenia. Zaznaczone oznacza zatrzymane
  odtwarzanie, odznaczone — włączone odtwarzanie. Kliknięcie zatrzymuje/wznawia
  radio z dotychczasową głośnością, nie zmienia procentu na suwaku i nie zamyka
  programu. Zaznaczenie odpowiada też zmianom z głównego okna i wyborowi stacji;
  pozycja zakończenia programu pozostaje na dole. PL/EN i klawiatura działają
  również dla tej opcji (Mateusz 11.11, po akceptacji wyglądu i dopasowania menu).

## Dane i prywatność

- Ustawienia użytkownika są lokalne dla konta i wydania; stacje i głośność nie są
  przesyłane do autora. Brak kont użytkowników, bazy i nowej telemetrii.
- Dane wydawcy i kontaktu wewnętrznego, identyfikatory i źródło aktualizacji są
  w ignorowanym pliku lokalnym. Nie wpisujemy ich do publicznej dokumentacji ani kodu.
- Klucze prywatne są poza repo w istniejącym magazynie. Agent nie odczytuje,
  nie wypisuje ani nie kopiuje ich zawartości. Kopię w Bitwardenie wykonuje Mateusz.
- Nową bibliotekę do podpisów dobieramy po sprawdzeniu jej istnienia i popularności;
  przed instalacją podajemy Mateuszowi nazwę i link.

## Poza zakresem

- Nowe formaty audio, nagrywanie, przebudowa katalogu i funkcje słuchania poza
  opisanymi historiami.
- Konta, synchronizacja, zbieranie statystyk i obsługa innych systemów.
- Podpis Authenticode i zakup certyfikatu; podpis aktualizacji to osobny mechanizm.
- Uruchamianie aplikacji, instalatora i przejmowanie pulpitu przez agenta bez
  konkretnej zgody. Odbiór działania wykonuje Mateusz.
- Publikacja na udziale lub w Releases bez osobnego wyraźnego upoważnienia.

## Testy odbioru

Testy działania są do wykonania przez Mateusza. Agent może kompilować bez
uruchamiania aplikacji i wykonywać statyczną kontrolę składników builda.
Automaty wykonujące kod wymagają wcześniejszej zgody według zasad projektu.

| Test | Co zrobić | Czego oczekuję | Warunek | Sposób |
|---|---|---|---|---|
| T27 | Schować okno, otworzyć menu zasobnika; przeciągnąć od razu widoczny suwak w obie strony; powtórzyć przy skalowaniu Windows 100%, 150% i 200% | Suwak obok wszystkich opcji, bez podmenu i nakładania tekstów; dźwięk zmienia się od razu, menu nie znika, okno pozostaje schowane; klik poza menu / Escape zamyka je | K7.1–K7.2 | Ręcznie |
| T28 | Ustawić 20% w oknie, 70% w trayu, zamknąć program poleceniem „Zakończ” i uruchomić ponownie | Oba miejsca pokazują tę samą wartość; po restarcie nadal 70% | K7.3 | Ręcznie |
| T29 | Zatrzymać radio, ustawić 0%, wznowić, następnie ustawić 100% | Regulacja nie wznawia radia; 0% wycisza, 100% daje maksimum radia; głośność systemowa pozostaje bez zmian | K7.4 | Ręcznie |
| T30 | W menu nacisnąć Tab, użyć strzałek i Escape; ponownie wejść do suwaka i nacisnąć Tab, wybrać stację; sprawdzić menu w PL i EN | Sterowanie klawiaturą działa, właściwy język, dotychczasowe polecenia działają | K7.5 | Ręcznie |
| T31 | Otworzyć menu w PL/EN przy 100%, 150% i 200% skali, także na mniejszym ekranie; obejrzeć wszystkie krawędzie, wskazać opcję myszą i klawiaturą, przeciągnąć suwak | Całe kompaktowe menu mieści się w obszarze roboczym monitora; wszystkie opcje widoczne bez przewijania, strzałek u góry/dołu, pustego pasa i obciętych napisów; czytelny procent, zaznaczona stacja, podświetlenie i fokus; regulacja nadal działa | K7.6 | Ręcznie |
| T32 | Ustawić 55%, zaznaczyć „Wycisz radio”, otworzyć menu ponownie, odznaczyć; potem zatrzymać/wznowić z okna i zmienić stację; powtórzyć klawiaturą i w EN | Zaznaczenie oznacza zatrzymanie; odznaczenie wznawia z zachowanymi 55%; stan zgodny z oknem, wybór stacji wznawia radio jak wcześniej i usuwa zaznaczenie; kontrolka dostępna myszą i klawiaturą, właściwy język; „Wyłącz radioMAT” nadal na dole | K7.7 | Ręcznie |
| T1 | Przygotować każdy wariant i pominąć wybór wydania | Wybór wymagany; wspólna wersja | K1.1 | Kontrola budowania |
| T2 | Użyć brakującej, pustej i błędnej konfiguracji wewnętrznej | Zatrzymanie przed kompilacją, bez publicznych zamienników | K1.2 | Kontrola budowania |
| T3 | Przeskanować paczkę i instrukcję instalatora wewnętrznego | CZYSTO, marka fizycznie nieobecna | K1.3 | Kontrola statyczna |
| T4 | Przeskanować paczkę i migawkę publiczną | CZYSTO, brak danych wewnętrznych | K1.4 | Kontrola statyczna |
| T5 | Porównać konfiguracje; zainstalować oba wydania | Odrębne tożsamości i ustawienia | K1.5 | Kontrola + ręcznie |
| T6 | Zainstalować na Windows bez osobnego .NET | Działa bez administratora | K2.1 | Ręcznie |
| T7 | Zmienić stacje i głośność, włączyć autostart, zaktualizować | Ustawienia zostają w tej odmianie | K2.2 | Ręcznie |
| T8 | Słuchać, zmienić skróty i stację w trayu, uruchomić drugi raz, zalogować się ponownie | Dotychczasowe zachowania działają | K2.3 | Ręcznie |
| T9 | Instalować, aktualizować i odinstalować przy ścieżce ze spacjami i ąę | Brak błędów; własny autostart usunięty; ustawienia zostają | K2.4 | Ręcznie |
| T10 | Sprawdzić aktualizacje przy starcie i z menu obu wydań | Właściwe źródło | K3.1 | Ręcznie |
| T11 | Udostępnić nowszą wersję, obejrzeć opis, pominąć i wybrać „Później” | Właściwe opcje i zapamiętanie pominięcia | K3.2 | Ręcznie |
| T12 | Podać brak podpisu, zły klucz i zmodyfikowany instalator | Odrzucenie każdej próby | K3.3 | Ręcznie / automat po zgodzie |
| T13 | Odłączyć sieć, odebrać dostęp, podać uszkodzony opis | Radio działa, widoczny komunikat | K3.4 | Ręcznie |
| T14 | Dwukrotnie kliknąć aktualizację | Jedna instalacja, poprawny powrót | K3.5 | Ręcznie |
| T15 | Podać pusty opis, ścieżkę poza źródłem i tekst `<b>x</b>` | Bez niepożądanego uruchomienia i formatowania | K3.6 | Ręcznie / automat po zgodzie |
| T16 | Otworzyć „O programie” i licencje obu odmian | Właściwa wersja, tożsamość i licencje | K4.1 | Ręcznie |
| T17 | Obejrzeć nagłówek i kliknąć filiżankę | Właściwy link tylko w odmianie publicznej | K4.2 | Ręcznie |
| T18 | Otworzyć kontakt z okna i zasobnika obu odmian | Właściwy kanał, bez wysyłki prywatnych danych | K4.3 | Ręcznie |
| T19 | Przełączyć PL/EN, uruchomić ponownie i sprawdzić instalator | Pełne tłumaczenia i zapisany wybór | K5.1 | Ręcznie |
| T20 | Wyświetlić długie nazwy i ICY w obu językach | Oryginalny tekst, czytelne kontrolki | K5.2 | Ręcznie |
| T21 | Przygotować pakiet wewnętrzny bez publikacji | Komplet czterech plików, opis publikowany na końcu | K6.1 | Kontrola przygotowania |
| T22 | Sprawdzić gotowe pliki, stronę i kolejność publikacji | Pełny komplet i osobna zgoda na publikację | K6.2 | Ręcznie |
| T23 | Porównać stan prac z centralą i skrzynką | Rzeczywiste wyniki, brak przedwczesnego zamknięcia | K6.3 | Kontrola dokumentacji |
| T24 | Wpisać do opisu wewnętrznego „wydanie publiczne” / „wsparcie autora”, a do publicznego „wydanie wewnętrzne”; zbudować | Build staje przed instalatorem; `opis` w najnowsza.json wewnętrznego bez tych słów | K1.6 | Kontrola budowania |
| T26 | Uruchomić `tools/Verify-Published.ps1` na opublikowanym wydaniu i na kopii z podmienionym plikiem / starszą wersją w opisie | Opublikowane: OK + lista rzeczy do aktualizacji; kopia: błąd z nazwą pliku | K6.4 | Kontrola publikacji |
| T25 | Do folderu zainstalowanej starszej wersji dodać plik `stary.dll`, zaktualizować instalatorem nowej | `stary.dll` znika, program działa, stacje/głośność/autostart zostają, odinstalowanie nadal działa | K2.5 | Ręcznie |

## Otwarte pytania przed odpowiednim etapem

- Dane wydawcy i kontaktu wewnętrznego: pobrać z właściwej lokalnej konfiguracji
  lub uzupełnić lokalnie z Mateuszem; nie zgadywać i nie publikować.
- Opis „Co nowego” osobno: RELEASE-NOTES.Company.md (wewnętrzny) i RELEASE-NOTES.Public.md (publiczny).
- Końcowy odbiór dotychczasowej 0.2.0: potwierdzono długie odtwarzanie i autostart;
  pozostałe zachowania wymagają potwierdzenia użytkownika.

## Kolejność realizacji

1. H1: konfiguracja wydania i bezpieczne przygotowanie builda bez publikacji.
2. H3: odrębne klucze, weryfikacja podpisów i aktualizator, potem instalator H2.
3. H4–H5: informacje, kontakt, marka i tłumaczenia publiczne.
4. Odbiór użytkownika i H6: wydanie wewnętrzne, potem publiczne i strona.
