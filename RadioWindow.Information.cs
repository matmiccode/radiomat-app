using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using Button = System.Windows.Controls.Button;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace Radiomat;

public partial class RadioWindow
{
    private readonly UpdateService _updates = new();
    private bool _updatesBusy;
    private Forms.ToolStripMenuItem? _trayMute;
    private string? _trackKey = "CheckingTitle", _statusTipKey;
    private string _statusKey = "Ready";

    private void SetTrackMessage(string key)
    {
        _trackKey = key; NowPlaying.Text = Strings.T(key); NowPlaying.ToolTip = null;
    }
    private void ConfigureInformation()
    {
        var edition = EditionConfiguration.Current;
        VersionLabel.Text = (edition.IsPublic ? edition.Publisher + " · " : "") + Strings.T("Version", UpdateService.VersionText);
        if (edition.IsPublic)
        {
            var button = new Button { Content = edition.CoffeeGlyph, ToolTip = Strings.Current.English ? edition.CoffeeLabelEn : edition.CoffeeLabelPl,
                FontSize = 21, Padding = new Thickness(8, 5, 0, 5), Background = Brushes.Transparent, Foreground = new SolidColorBrush(Color.FromRgb(101, 114, 139)) };
            System.Windows.Automation.AutomationProperties.SetName(button, button.ToolTip.ToString());
            button.Click += (_, _) => OpenLink(edition.CoffeeUri);
            BrandAction.Content = button;
        }
    }
    private void BuildTrayMenu()
    {
        var previous = _tray.ContextMenuStrip;
        var menu = new TrayVolumeMenu((int)Math.Round(VolumeSlider.Value));
        menu.VolumeChanged += value => VolumeSlider.Value = value;
        menu.Commands.Add(Strings.T("Show"), null, (_, _) => Restore());
        menu.Commands.Add(new Forms.ToolStripSeparator());
        for (int i = 0; i < 3; i++)
        {
            var slot = i;
            _trayFavorites[i] = new Forms.ToolStripMenuItem();
            _trayFavorites[i].Click += (_, _) => SelectShortcut(slot);
            menu.Commands.Add(_trayFavorites[i]);
        }
        menu.Commands.Add(new Forms.ToolStripSeparator());
        _trayMute = new Forms.ToolStripMenuItem(Strings.T("MuteRadio")) { CheckOnClick = true };
        _trayMute.Click += (_, _) => TogglePlayback();
        menu.Commands.Add(_trayMute);
        UpdateTrayPlayback();
        menu.Commands.Add(Strings.T("CheckUpdates"), null, async (_, _) => await CheckUpdatesAsync(true));
        menu.Commands.Add(Strings.T("About"), null, (_, _) => ShowAbout());
        menu.Commands.Add(Strings.T("Report"), null, (_, _) => OpenLink(EditionConfiguration.Current.SupportUri));
        if (EditionConfiguration.Current.IsPublic)
        {
            menu.Commands.Add(Strings.Current.English ? EditionConfiguration.Current.CoffeeLabelEn : EditionConfiguration.Current.CoffeeLabelPl,
                null, (_, _) => OpenLink(EditionConfiguration.Current.CoffeeUri));
            menu.Commands.Add("PL / EN", null, (_, _) => ChangeLanguage());
        }
        menu.Commands.Add(new Forms.ToolStripSeparator());
        menu.Commands.Add(Strings.T("Exit"), null, (_, _) => Exit());
        _tray.ContextMenuStrip = menu;
        previous?.Dispose();
    }
    private void UpdateTrayPlayback()
    {
        if (_trayMute == null) return;
        _trayMute.Checked = _audio == null;
        _trayMute.ToolTipText = Strings.T(_audio == null ? "RadioMutedHint" : "RadioPlayingHint");
    }
    private void ChangeLanguage()
    {
        var previous = _settings.Language;
        _settings.Language = Strings.Current.English ? "pl" : "en";
        if (!SaveSettings()) { _settings.Language = previous; Notify(Strings.T("AssignFailed"), false); return; }
        Strings.Current.SetLanguage(_settings.Language);
        BuildTrayMenu(); ConfigureInformation(); UpdateShortcuts(); FilterCatalog();
        PlayButton.Content = Strings.T(_audio == null ? "Play" : "Stop");
        if (_trackKey != null) NowPlaying.Text = Strings.T(_trackKey);
        StatusText.Text = Strings.T(_statusKey);
        StatusBadge.ToolTip = _statusTipKey == null ? null : Strings.T(_statusTipKey);
        CatalogMessage.Text = Strings.T("SelectStation");
        NotificationBanner.Visibility = Visibility.Collapsed;
    }
    private void AboutClick(object sender, RoutedEventArgs e) => ShowAbout();
    private void ReportClick(object sender, RoutedEventArgs e) => OpenLink(EditionConfiguration.Current.SupportUri);
    private void OpenLink(string target)
    {
        try { using var process = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        { Notify(Strings.T("LinkFailed"), false); }
    }
    private Window SmallWindow(string title, out StackPanel body)
    {
        Restore();
        body = new StackPanel { Margin = new Thickness(24) };
        return new Window { Title = title, Icon = Icon, Owner = this, Width = 410, SizeToContent = SizeToContent.Height,
            MaxHeight = 560, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            FontFamily = FontFamily, FontSize = 14, Background = new SolidColorBrush(Color.FromRgb(242, 245, 250)),
            Foreground = Foreground, Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
    }
    private static TextBlock Paragraph(string text, int size = 14) => new()
    { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
    private static Button ActionButton(string text, Action action)
    {
        var button = new Button { Content = text, Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 3, 0, 3),
            HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center };
        button.Click += (_, _) => action();
        return button;
    }
    private void ShowAbout()
    {
        var dialog = SmallWindow(Strings.T("About"), out var body);
        body.Children.Add(Paragraph("radioMAT", 25));
        body.Children.Add(Paragraph(Strings.T("Version", UpdateService.VersionText)));
        body.Children.Add(Paragraph(Strings.T("Description")));
        var edition = EditionConfiguration.Current;
        if (edition.IsPublic)
        {
            body.Children.Add(Paragraph(edition.Publisher));
            body.Children.Add(ActionButton(Strings.T("Website"), () => OpenLink(edition.PublisherUrl)));
            body.Children.Add(ActionButton("PL / EN", () => { dialog.Close(); ChangeLanguage(); ShowAbout(); }));
        }
        else body.Children.Add(Paragraph(Strings.T("Maintainer", edition.Publisher)));
        body.Children.Add(ActionButton(Strings.T("Licenses"), () => OpenLink(Path.Combine(AppContext.BaseDirectory, "licenses"))));
        body.Children.Add(ActionButton(Strings.T("CheckUpdates"), () => { dialog.Close(); _ = CheckUpdatesAsync(true); }));
        body.Children.Add(ActionButton(Strings.T("Report"), () => OpenLink(edition.SupportUri)));
        body.Children.Add(ActionButton(Strings.T("Close"), dialog.Close));
        dialog.ShowDialog();
    }

    internal async void StartUpdateCheck(bool failed)
    {
        if (failed) { Notify(Strings.T("UpdateInstallFailed"), false); return; }
        await Task.Delay(2500);
        if (!_exiting) await CheckUpdatesAsync(false);
    }
    private async Task CheckUpdatesAsync(bool manual)
    {
        if (_updatesBusy || _exiting) return;
        _updatesBusy = true;
        try
        {
            if (manual) Notify(Strings.T("CheckingUpdates"));
            var offer = await _updates.CheckAsync();
            if (_exiting) return;
            if (offer == null) { if (manual) Notify(Strings.T("NoRelease")); return; }
            if (offer.Version <= UpdateService.CurrentVersion) { if (manual) Notify(Strings.T("UpToDate")); return; }
            if (!manual && offer.Version.ToString(3) == _settings.SkippedUpdate) return;
            if (!manual && !IsVisible)
            {
                Notify(Strings.T("UpdateAvailable", offer.Version.ToString(3)));
                return; // Tray menu's explicit check opens the update dialog.
            }
            var dialog = SmallWindow(Strings.T("UpdateAvailable", offer.Version.ToString(3)), out var body);
            body.Children.Add(Paragraph(Strings.T("UpdateAvailable", offer.Version.ToString(3)), 21));
            body.Children.Add(Paragraph(Strings.T("WhatsNew"), 16));
            body.Children.Add(new ScrollViewer { MaxHeight = 210, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = Paragraph(offer.Notes) });
            bool install = false;
            body.Children.Add(ActionButton(Strings.T("UpdateNow"), () => { install = true; dialog.Close(); }));
            body.Children.Add(ActionButton(Strings.T("Skip"), () =>
            {
                var previous = _settings.SkippedUpdate;
                _settings.SkippedUpdate = offer.Version.ToString(3);
                if (SaveSettings()) { dialog.Close(); Notify(Strings.T("UpdateSkipped", _settings.SkippedUpdate)); }
                else { _settings.SkippedUpdate = previous; Notify(Strings.T("AssignFailed"), false); }
            }));
            body.Children.Add(ActionButton(Strings.T("Later"), dialog.Close));
            dialog.ShowDialog();
            if (!install || _exiting) return;
            Notify(Strings.T("UpdateDownloading"));
            var downloaded = await _updates.DownloadAsync(offer);
            if (_exiting)
            {
                try { File.Delete(downloaded.Path); Directory.Delete(Path.GetDirectoryName(downloaded.Path)!); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                return;
            }
            try { UpdateService.LaunchInstaller(downloaded.Path, downloaded.Hash); }
            catch { try { File.Delete(downloaded.Path); Directory.Delete(Path.GetDirectoryName(downloaded.Path)!); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } throw; }
            Exit();
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException or FormatException
            or ArgumentException or InvalidOperationException or KeyNotFoundException or System.Security.Cryptography.CryptographicException)
        { if (!_exiting) Notify(Strings.T("UpdateRejected"), false); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Net.Http.HttpRequestException
            or OperationCanceledException or System.ComponentModel.Win32Exception or DllNotFoundException or TypeInitializationException)
        { if (!_exiting) Notify(Strings.T(EditionConfiguration.Current.IsPublic ? "UpdateOffline" : "FolderOffline"), false); }
        finally { _updatesBusy = false; }
    }
}
