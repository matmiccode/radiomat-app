# Katalog stacji

Radio Plus Lublin jest dołączone niezależnie od migawki jako domyślna stacja: `https://ic1.smcdn.pl/4130-1.mp3`. Przypisanie tego strumienia do Lublina sprawdzono w wykazie https://emsoft.ct8.pl/strumienie.php?mode=m (2026-11-03); regionalna strona nadawcy: https://www.radioplus.pl/lublin/. Nie wykonano testu odsłuchu.

`poland.json`: migawka katalogu Radio Browser z 2026-11-03. Źródło danych: https://www.radio-browser.info/ (dane public domain), dokumentacja: https://docs.radio-browser.info/.

Pobrano polskie stacje MP3 oznaczone przez katalog jako działające; odrzucono HLS, powtórzone nazwy i adresy. Obecny silnik obsługuje bezpośrednie MP3, dlatego lista nie obejmuje stacji dostępnych wyłącznie jako AAC, Ogg lub HLS. Wpis katalogowy nie gwarantuje bieżącej dostępności strumienia; odtwarzania wpisów nie testowano.

Konwersja lokalnie pobranego pliku `Data/poland.raw.json`: `python tools/import_catalog.py`. Dane mają wyłącznie charakter katalogowy, nie zawierają logotypów ani nagrań. Aplikacja nie odpytuje serwerów stacji podczas przeglądania listy.
