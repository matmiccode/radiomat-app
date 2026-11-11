using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using System.Globalization;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using Application = System.Windows.Application;
namespace Radiomat;

internal sealed record Station(string Name, string Url, string Description, string Id = "");
internal sealed class Settings
{
    public string Language { get; set; } = "pl";
    public string? SkippedUpdate { get; set; }
    public int StationIndex { get; set; }
    public double Volume { get; set; } = .55;
    public Station[]? Favorites { get; set; }
    public Station? LastStation { get; set; }
}
internal static class Program
{
    [STAThread] private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--verify-audio") return Verify(args[1]).GetAwaiter().GetResult();
        EditionConfiguration.SetWindowsIdentity();
        bool autostart = args.Contains("--autostart", StringComparer.OrdinalIgnoreCase);
        using var instance = new SingleInstance();
        if (!instance.TryAcquire()) { if (!autostart) instance.ShowExisting(); return 0; }
        // A small, static radio window does not need a Direct3D device. Avoid
        // loading GPU-driver pools; keep the scope limited to this process.
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new RadioWindow();
        instance.Listen(() => app.Dispatcher.BeginInvoke(window.Restore));
        app.MainWindow = window;
        app.Startup += (_, _) => { if (!autostart) window.Show(); window.StartPlayback(); window.StartUpdateCheck(args.Contains("--update-failed")); };
        app.Run(); return 0;
    }
    private static async Task<int> Verify(string path)
    {
        var report = new List<string>(); bool success = true;
        foreach (var station in RadioWindow.Stations)
        {
            using var audio = new RadioAudio { Volume = 0 }; string? error = null;
            audio.Failed += ex => error = ex.ToString();
            var task = audio.StartAsync(station.Url);
            for (int i = 0; i < 200 && audio.DecodedBytes < 500000 && error == null; i++) await Task.Delay(100);
            bool ok = audio.DecodedBytes >= 500000; success &= ok;
            report.Add($"{station.Name}: {(ok ? "PASS" : "FAIL")}; PCM={audio.DecodedBytes}; {error}");
            audio.Dispose(); await task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        await File.WriteAllLinesAsync(path, report); return success ? 0 : 1;
    }
}
public partial class RadioWindow : Window
{
    internal static readonly Station[] Stations =
    [
        new("Radio Plus Lublin", "https://ic1.smcdn.pl/4130-1.mp3", "Lublin · Przeboje z nutą nostalgii", "curated:plus-lublin"),
        new("RMF FM", "https://rs201-krk.rmfstream.pl/rmf_fm", "Muzyka i fakty"),
        new("Radio ZET", "https://zt.cdn.eurozet.pl/zet-net.mp3", "Siła muzyki")
    ];
    private static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), EditionConfiguration.Current.SettingsDirectory, "settings.json");
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly Settings _settings;
    private readonly IReadOnlyList<Station> _catalog;
    private readonly Station[] _favorites;
    private Station _current;
    private RadioAudio? _audio;
    private readonly Forms.NotifyIcon _tray;
    private readonly System.Drawing.Icon _trayIcon;
    private readonly Forms.ToolStripMenuItem[] _trayFavorites = new Forms.ToolStripMenuItem[3];
    private readonly DispatcherTimer _retry = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly DispatcherTimer _notificationTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool _exiting, _ready;
    public RadioWindow()
    {
        _settings = LoadSettings();
        Strings.Current.SetLanguage(_settings.Language);
        _catalog = StationCatalog.Load(Stations);
        _favorites = Enumerable.Range(0, 3).Select(i =>
            _settings.Favorites is { Length: 3 } && IsValid(_settings.Favorites[i]) ? MigrateDefaultPlus(_settings.Favorites[i]) : Stations[i]).ToArray();
        _current = IsValid(_settings.LastStation) ? MigrateDefaultPlus(_settings.LastStation!) : _favorites[Math.Clamp(_settings.StationIndex, 0, 2)];
        InitializeComponent(); VolumeSlider.Value = Math.Clamp(_settings.Volume * 100, 0, 100);
        StartupToggle.IsChecked = IsStartupEnabled();
        using (var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/radioMAT.ico")).Stream)
        using (var icon = new System.Drawing.Icon(iconStream)) _trayIcon = (System.Drawing.Icon)icon.Clone();
        _tray = new Forms.NotifyIcon { Icon = _trayIcon, Text = "radioMAT", Visible = true };
        BuildTrayMenu();
        _tray.DoubleClick += (_, _) => Restore(); _retry.Tick += (_, _) => Play();
        _notificationTimer.Tick += (_, _) => { _notificationTimer.Stop(); NotificationBanner.Visibility = Visibility.Collapsed; };
        Closing += (_, e) => { if (!_exiting) { e.Cancel = true; Hide(); } };
        _ready = true; UpdateShortcuts(); FilterCatalog(); ConfigureInformation();
    }
    internal void Restore() { Show(); WindowState = WindowState.Normal; Activate(); }
    internal void StartPlayback() => Play();
    private void StationClick(object sender, RoutedEventArgs e)
    {
        var slot = int.Parse((string)((System.Windows.Controls.RadioButton)sender).Tag);
        SelectShortcut(slot);
    }
    private void SelectShortcut(int slot)
    {
        _settings.StationIndex = slot; _current = _favorites[slot]; SaveSettings(); Play();
    }
    private void PlaybackClick(object sender, RoutedEventArgs e) => TogglePlayback();
    private void TogglePlayback() { if (_audio != null) Stop(); else Play(); }
    private void VolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        _settings.Volume = e.NewValue / 100.0;
        if (_tray.ContextMenuStrip is TrayVolumeMenu menu) menu.SetVolume(e.NewValue);
        if (_audio != null) _audio.Volume = (float)_settings.Volume; SaveSettings();
    }
    private void Play()
    {
        _retry.Stop(); _audio?.Dispose();
        var audio = new RadioAudio { Volume = (float)_settings.Volume }; _audio = audio;
        StationName.Text = _current.Name; StationDescription.Text = _current.Description;
        StationName.ToolTip = _current.Name; StationDescription.ToolTip = _current.Description;
        SetTrackMessage("CheckingTitle");
        SetStatus("Connecting", "#FFF2D3", "#8C5A08", "PreparingAudio");
        PlayButton.Content = Strings.T("Stop");
        UpdateTrayPlayback();
        UpdateShortcuts();
        audio.TrackChanged += title => Dispatcher.BeginInvoke(() =>
        {
            if (_audio != audio) return;
            _trackKey = title == null ? "WaitingTitle" : string.IsNullOrWhiteSpace(title) ? "NoTitle" : null;
            NowPlaying.Text = _trackKey == null ? title : Strings.T(_trackKey);
            NowPlaying.ToolTip = NowPlaying.Text;
        });
        audio.Status += _ => Dispatcher.BeginInvoke(() =>
        { if (_audio == audio) SetStatus("Live", "#E4F5EB", "#227347", "LiveHint"); });
        audio.Failed += ex => Dispatcher.BeginInvoke(() =>
        {
            if (_audio != audio) return;
            SetStatus("Retry", "#FFF2D3", "#8C5A08"); StatusBadge.ToolTip = Strings.T("StationFailed", ex.Message); _retry.Start();
        });
        _ = audio.StartAsync(_current.Url);
    }
    private void Stop()
    {
        _retry.Stop(); _audio?.Dispose(); _audio = null;
        SetTrackMessage("PlaybackStopped"); SetStatus("Stopped", "#E9EDF3", "#65728B");
        PlayButton.Content = Strings.T("Play"); UpdateTrayPlayback();
    }
    private void Exit() { _exiting = true; _updates.Dispose(); Stop(); _notificationTimer.Stop(); _tray.ContextMenuStrip?.Dispose(); _tray.Dispose(); _trayIcon.Dispose(); Application.Current.Shutdown(); }
    private static Settings LoadSettings()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath)) ?? new(); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }
    private bool SaveSettings()
    {
        try
        {
            _settings.Favorites = _favorites; _settings.LastStation = _current;
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!); File.WriteAllText(SettingsPath, JsonSerializer.Serialize(_settings));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { StatusText.ToolTip = Strings.T("SaveFailed", ex.Message); return false; }
    }
    private static bool IsStartupEnabled()
    {
        try { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue(EditionConfiguration.Current.RunValueName) is string; }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException) { return false; }
    }
    private void StartupChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        bool enabled = StartupToggle.IsChecked == true;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled) key.SetValue(EditionConfiguration.Current.RunValueName, $"\"{Environment.ProcessPath ?? throw new IOException(Strings.T("MissingPath"))}\" --autostart"); else key.DeleteValue(EditionConfiguration.Current.RunValueName, false);
            Notify(Strings.T(enabled ? "StartupOn" : "StartupOff"));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            _ready = false; StartupToggle.IsChecked = !enabled; _ready = true;
            Notify(Strings.T("StartupFailed"), false);
        }
    }

    private static bool IsValid(Station? station) => station != null && !string.IsNullOrWhiteSpace(station.Name)
        && Uri.TryCreate(station.Url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    private static Station MigrateDefaultPlus(Station station) =>
        station.Name == "Radio Plus" && station.Url == "https://pl03.cdn.eurozet.pl/plu-waw.mp3" ? Stations[0] : station;

    private void SetStatus(string text, string background, string foreground, string? tooltip = null)
    {
        var converter = new System.Windows.Media.BrushConverter();
        StatusBadge.Background = (System.Windows.Media.Brush)converter.ConvertFromString(background)!;
        var ink = (System.Windows.Media.Brush)converter.ConvertFromString(foreground)!;
        _statusKey = text; _statusTipKey = tooltip;
        StatusText.Text = Strings.T(text); StatusText.Foreground = ink; StatusDot.Fill = ink;
        StatusBadge.ToolTip = tooltip == null ? null : Strings.T(tooltip);
    }

    private void UpdateShortcuts()
    {
        var buttons = new[] { StationPlus, StationRmf, StationZet };
        for (int i = 0; i < 3; i++)
        {
            buttons[i].Content = _favorites[i].Name; buttons[i].ToolTip = $"{i + 1}. {_favorites[i].Name}";
            buttons[i].IsChecked = _favorites[i].Url == _current.Url;
            _trayFavorites[i].Text = $"{i + 1}. {_favorites[i].Name.Replace("&", "&&")}";
            _trayFavorites[i].Checked = buttons[i].IsChecked == true;
        }
        var tooltip = "radioMAT — " + _current.Name;
        _tray.Text = tooltip.Length > 63 ? tooltip[..60] + "…" : tooltip;
        PinOne.ToolTip = Strings.T("Replace", _favorites[0].Name);
        PinTwo.ToolTip = Strings.T("Replace", _favorites[1].Name);
        PinThree.ToolTip = Strings.T("Replace", _favorites[2].Name);
    }
    private void OpenCatalog(object sender, RoutedEventArgs e)
    {
        PlayerPanel.Visibility = Visibility.Collapsed; CatalogPanel.Visibility = Visibility.Visible;
        FilterCatalog(); CatalogSearch.Focus();
    }
    private void CloseCatalog(object sender, RoutedEventArgs e)
    { CatalogPanel.Visibility = Visibility.Collapsed; PlayerPanel.Visibility = Visibility.Visible; }
    private void SearchChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    { if (_ready) FilterCatalog(); }
    private void FilterCatalog()
    {
        var query = CatalogSearch.Text.Trim();
        var compare = CultureInfo.GetCultureInfo("pl-PL").CompareInfo;
        var rows = _catalog.Where(s => query.Length == 0 || compare.IndexOf(s.Name + " " + s.Description,
            query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0).ToArray();
        CatalogList.ItemsSource = rows;
        CatalogCount.Text = Strings.T("StationCount", rows.Length);
    }
    private void CatalogSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        bool selected = CatalogList.SelectedItem is Station;
        ListenButton.IsEnabled = PinOne.IsEnabled = PinTwo.IsEnabled = PinThree.IsEnabled = selected;
        CatalogMessage.Text = selected ? ((Station)CatalogList.SelectedItem).Name : Strings.T("SelectStation");
    }
    private void ListenSelected(object sender, RoutedEventArgs e)
    {
        if (CatalogList.SelectedItem is not Station station) return;
        _current = station; SaveSettings(); Play(); CloseCatalog(sender, e);
    }
    private void PinSelected(object sender, RoutedEventArgs e)
    {
        if (CatalogList.SelectedItem is not Station station) return;
        int slot = int.Parse((string)((System.Windows.Controls.Button)sender).Tag);
        var previous = _favorites[slot];
        _favorites[slot] = station;
        if (!SaveSettings())
        {
            _favorites[slot] = previous;
            Notify(Strings.T("AssignFailed"), false);
            return;
        }
        UpdateShortcuts(); CatalogMessage.Text = Strings.T("ButtonStation", slot + 1, station.Name);
        Notify(Strings.T("Assigned", station.Name, slot + 1));
    }

    private void Notify(string message, bool success = true)
    {
        if (_exiting) return;
        if (!IsVisible) _tray.ShowBalloonTip(5000, "radioMAT", message, success ? Forms.ToolTipIcon.Info : Forms.ToolTipIcon.Warning);
        _notificationTimer.Stop();
        var converter = new System.Windows.Media.BrushConverter();
        NotificationBanner.Background = (System.Windows.Media.Brush)converter.ConvertFromString(success ? "#E4F5EB" : "#FFF2D3")!;
        NotificationText.Foreground = (System.Windows.Media.Brush)converter.ConvertFromString(success ? "#227347" : "#8C5A08")!;
        NotificationText.Text = message;
        NotificationBanner.Visibility = Visibility.Visible;
        var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.FromElement(NotificationText)
            ?? System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(NotificationText);
        peer?.RaiseAutomationEvent(System.Windows.Automation.Peers.AutomationEvents.LiveRegionChanged);
        _notificationTimer.Start();
    }
}
