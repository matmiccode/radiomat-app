using System.ComponentModel;
using System.Globalization;

namespace Radiomat;

public sealed class Strings : INotifyPropertyChanged
{
    public static Strings Current { get; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    public bool English { get; private set; }
    public string this[string key] => Text.TryGetValue(key, out var pair) ? (English ? pair.En : pair.Pl) : key;
    internal static string T(string key, params object[] values) => string.Format(CultureInfo.CurrentCulture, Current[key], values);
    internal void SetLanguage(string language)
    {
        English = EditionConfiguration.Current.IsPublic && language == "en";
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(English ? "en-US" : "pl-PL");
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
    private static readonly Dictionary<string, (string Pl, string En)> Text = new()
    {
        ["Tagline"] = ("Twój rytm dnia.", "Your daily rhythm."),
        ["NowOnAir"] = ("Teraz na antenie", "Now playing"),
        ["CheckingTitle"] = ("Sprawdzam tytuł utworu…", "Checking track title…"),
        ["WaitingTitle"] = ("Oczekiwanie na tytuł…", "Waiting for track title…"),
        ["NoTitle"] = ("Stacja nie podaje tytułu utworu", "This station does not provide track titles"),
        ["Connecting"] = ("Łączenie ze stacją", "Connecting to station"),
        ["PreparingAudio"] = ("Trwa pobieranie i przygotowanie dźwięku.", "Downloading and preparing audio."),
        ["Live"] = ("Gramy na żywo", "Playing live"),
        ["LiveHint"] = ("Odtwarzana jest bieżąca transmisja radiowa.", "Playing the live radio broadcast."),
        ["Retry"] = ("Ponawiam za 10 sekund", "Retrying in 10 seconds"),
        ["StationFailed"] = ("Nie udało się odtworzyć stacji: {0}", "Could not play this station: {0}"),
        ["Stopped"] = ("Radio zatrzymane", "Radio stopped"),
        ["PlaybackStopped"] = ("Odtwarzanie zatrzymane", "Playback stopped"),
        ["Ready"] = ("Gotowe do słuchania", "Ready to play"),
        ["Play"] = ("Włącz radio", "Play radio"),
        ["Stop"] = ("Zatrzymaj radio", "Stop radio"),
        ["MoreStations"] = ("Więcej stacji", "More stations"),
        ["Volume"] = ("Głośność", "Volume"),
        ["VolumeHint"] = ("Głośność w procentach", "Volume in percent"),
        ["Startup"] = ("Graj po uruchomieniu Windows", "Play when Windows starts"),
        ["StartupHint"] = ("Dla tego konta Windows. Po zalogowaniu radio zagra schowane przy zegarku.", "For this Windows account. Radio starts in the system tray after you sign in."),
        ["StartupOn"] = ("Autostart włączony. Po zalogowaniu radio zagra schowane przy zegarku.", "Autostart enabled. Radio will play in the system tray after you sign in."),
        ["StartupOff"] = ("Autostart wyłączony dla tego konta.", "Autostart disabled for this account."),
        ["StartupFailed"] = ("Nie udało się zmienić autostartu. Sprawdź uprawnienia konta Windows.", "Could not change autostart. Check your Windows account permissions."),
        ["Back"] = ("Wróć", "Back"),
        ["PolishStations"] = ("Polskie stacje", "Polish stations"),
        ["Search"] = ("Szukaj stacji lub miejscowości…", "Search by station or town…"),
        ["StationCount"] = ("{0} stacji · wybierz stację z listy", "{0} stations · choose a station"),
        ["SelectStation"] = ("Wybierz stację, żeby słuchać lub przypisać skrót.", "Choose a station to listen or assign a shortcut."),
        ["Listen"] = ("Słuchaj wybranej stacji", "Listen to selected station"),
        ["Assign"] = ("Przypisz do przycisku:", "Assign to button:"),
        ["CatalogCredit"] = ("Katalog Radio Browser • stacje MP3", "Radio Browser catalog • MP3 stations"),
        ["Replace"] = ("Zastąp: {0}", "Replace: {0}"),
        ["Assigned"] = ("Przypisano „{0}” do przycisku {1}.", "Assigned “{0}” to button {1}."),
        ["ButtonStation"] = ("Przycisk {0}: {1}", "Button {0}: {1}"),
        ["SaveFailed"] = ("Nie udało się zapisać ustawień: {0}", "Could not save settings: {0}"),
        ["AssignFailed"] = ("Nie udało się zapisać przypisania. Spróbuj ponownie.", "Could not save this shortcut. Please try again."),
        ["Show"] = ("Pokaż radioMAT", "Show radioMAT"),
        ["MuteRadio"] = ("Wycisz radio", "Mute radio"),
        ["RadioMutedHint"] = ("Radio zatrzymane. Odznacz, aby wznowić z dotychczasową głośnością.", "Radio stopped. Uncheck to resume at the previous volume."),
        ["RadioPlayingHint"] = ("Odtwarzanie włączone. Zaznacz, aby zatrzymać radio.", "Playback is on. Check to stop the radio."),
        ["Exit"] = ("Wyłącz radioMAT", "Exit radioMAT"),
        ["About"] = ("O programie", "About"),
        ["Report"] = ("Zgłoś problem", "Report a problem"),
        ["CheckUpdates"] = ("Sprawdź aktualizacje", "Check for updates"),
        ["Version"] = ("wersja {0}", "version {0}"),
        ["Description"] = ("Polskie stacje radiowe, trzy skróty i muzyka przy zegarku Windows.", "Polish radio stations, three shortcuts and music in your Windows system tray."),
        ["Website"] = ("Strona programu", "Program website"),
        ["Maintainer"] = ("Opiekun: {0}", "Maintainer: {0}"),
        ["Licenses"] = ("Licencje", "Licenses"),
        ["Close"] = ("Zamknij", "Close"),
        ["LinkFailed"] = ("Nie udało się otworzyć odnośnika lub folderu licencji.", "Could not open this link or the licenses folder."),
        ["UpdateAvailable"] = ("Dostępna nowa wersja {0}", "Version {0} is available"),
        ["WhatsNew"] = ("Co nowego", "What's new"),
        ["UpdateNow"] = ("Zaktualizuj teraz", "Update now"),
        ["Skip"] = ("Pomiń tę wersję", "Skip this version"),
        ["Later"] = ("Później", "Later"),
        ["CheckingUpdates"] = ("Sprawdzam aktualizacje…", "Checking for updates…"),
        ["UpToDate"] = ("Masz najnowszą wersję radioMAT.", "Your radioMAT is up to date."),
        ["NoRelease"] = ("Pierwsza aktualizacja nie została jeszcze opublikowana.", "The first update has not been published yet."),
        ["UpdateOffline"] = ("Nie udało się sprawdzić aktualizacji. Radio działa normalnie; spróbuj później.", "Could not check for updates. Radio keeps working; try again later."),
        ["FolderOffline"] = ("Ten komputer nie widzi folderu z aktualizacjami – program działa normalnie, nowa wersja przyjdzie, gdy komputer wróci do sieci firmy.", "The update folder is unavailable. Radio keeps working; updates will return when the folder is available."),
        ["UpdateInvalid"] = ("Opis aktualizacji jest niepoprawny. Radio działa normalnie.", "The update information is invalid. Radio keeps working."),
        ["UpdateDownloading"] = ("Pobieram i sprawdzam podpis aktualizacji…", "Downloading and verifying the update signature…"),
        ["UpdateRejected"] = ("Aktualizacja została odrzucona: brak poprawnego podpisu, sumy lub zgodnej wersji. Niczego nie zainstalowano.", "Update rejected: signature, checksum or version could not be verified. Nothing was installed."),
        ["UpdateInstallFailed"] = ("Nie udało się zainstalować aktualizacji. Radio działa w dotychczasowej wersji.", "Could not install the update. Radio is running the previous version."),
        ["UpdateSkipped"] = ("Pominięto wersję {0}.", "Skipped version {0}."),
        ["AudioEnded"] = ("Stacja zakończyła połączenie.", "The station closed the connection."),
        ["NotMp3"] = ("Stacja nie wysyła dźwięku MP3.", "The station is not sending MP3 audio."),
        ["MissingPath"] = ("Nie można ustalić ścieżki programu.", "Could not determine the application path.")
    };
}
