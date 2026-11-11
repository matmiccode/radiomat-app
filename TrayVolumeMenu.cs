using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using Forms = System.Windows.Forms;

namespace Radiomat;

// Keep NotifyIcon's Windows menu lifecycle; render its contents with the same
// WPF controls and palette as the player, through a supported control host.
internal sealed class TrayVolumeMenu : Forms.ContextMenuStrip
{
    private static readonly Color Surface = Color.FromArgb(242, 245, 250);
    private readonly Forms.ToolStrip _commands = new();
    private readonly TrayMenuView _view;
    private readonly ElementHost _elementHost;
    private readonly ContentHost _host;

    internal event Action<double>? VolumeChanged;
    internal Forms.ToolStripItemCollection Commands => _commands.Items;

    internal TrayVolumeMenu(int volume)
    {
        ShowImageMargin = ShowCheckMargin = false;
        AutoSize = false;
        Padding = Forms.Padding.Empty;
        BackColor = Surface;
        Renderer = new SurfaceRenderer();
        _view = new TrayMenuView();
        _view.VolumeChanged += value => VolumeChanged?.Invoke(value);
        _view.CloseRequested += () => Close(Forms.ToolStripDropDownCloseReason.Keyboard);
        _view.CommandInvoked += item =>
        {
            Close(Forms.ToolStripDropDownCloseReason.ItemClicked);
            item.PerformClick();
        };
        _elementHost = new ElementHost { Child = _view, AutoSize = false, BackColor = Surface };
        _host = new ContentHost(_elementHost)
        {
            AutoSize = false, Margin = Forms.Padding.Empty, Padding = Forms.Padding.Empty
        };
        Items.Add(_host);
        SetVolume(volume);
    }

    protected override Forms.Padding DefaultPadding => Forms.Padding.Empty;

    internal void SetVolume(double volume) => _view.SetVolume(volume);

    protected override void OnOpening(System.ComponentModel.CancelEventArgs e)
    {
        // ElementHost maps Windows Forms font defaults onto its child. Restore
        // the player's typography before measuring the WPF content.
        _view.FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable, Segoe UI");
        _view.FontSize = 12;
        _view.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(23, 36, 64));
        _view.SetCommands(Commands.Cast<Forms.ToolStripItem>());
        _view.LayoutTransform = System.Windows.Media.Transform.Identity;
        // WPF measures in logical units; the menu host uses device pixels.
        _view.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        double scale = DeviceDpi / 96.0;
        var workArea = Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
        double clearance = Math.Ceiling(16 * scale);
        double fit = Math.Min(1, Math.Min(
            Math.Max(1, workArea.Width - clearance) / (_view.DesiredSize.Width * scale),
            Math.Max(1, workArea.Height - clearance - 1) / (_view.DesiredSize.Height * scale)));
        if (fit < 1)
        {
            // Preserve every command and the slider together on smaller screens.
            _view.LayoutTransform = new System.Windows.Media.ScaleTransform(fit, fit);
            _view.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        }
        var size = new Size((int)Math.Ceiling(_view.DesiredSize.Width * scale),
            (int)Math.Ceiling(_view.DesiredSize.Height * scale) + 1);
        // Size includes the native window frame; the hosted panel must fit
        // ClientSize or ContextMenuStrip inserts unnecessary scroll arrows.
        SuspendLayout();
        try { ClientSize = size; FitHost(); }
        finally { ResumeLayout(true); }
        UpdateRoundedRegion();
        base.OnOpening(e);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _elementHost.Focus();
        _view.FocusFirstCommand();
    }

    protected override void SetDisplayedItems()
    {
        // This menu contains one panel, not a scrolling list of native rows.
        // Fit it after the native layout pass, before WinForms checks overflow.
        FitHost();
        base.SetDisplayedItems();
    }

    private void FitHost()
    {
        if (_host == null) return; // Base constructors can trigger layout.
        var bounds = ClientRectangle;
        // WinForms tests Contains(item.Bounds.Bottom) as a point, and Contains
        // excludes the bottom edge. Exact-height panels otherwise always scroll.
        bounds.Height = Math.Max(0, bounds.Height - 1);
        _host.Fill(bounds);
    }

    private void UpdateRoundedRegion()
    {
        int diameter = Math.Min((int)Math.Round(28 * DeviceDpi / 96.0), Math.Min(Width, Height));
        if (diameter <= 0) return;
        using var path = new GraphicsPath();
        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(Width - diameter, 0, diameter, diameter, 270, 90);
        path.AddArc(Width - diameter, Height - diameter, diameter, diameter, 0, 90);
        path.AddArc(0, Height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        var previous = Region;
        Region = new Region(path);
        previous?.Dispose();
    }

    protected override bool ProcessCmdKey(ref Forms.Message m, Forms.Keys keyData) =>
        HandleKey(keyData) || base.ProcessCmdKey(ref m, keyData);

    protected override bool ProcessDialogKey(Forms.Keys keyData) =>
        HandleKey(keyData) || base.ProcessDialogKey(keyData);

    private bool HandleKey(Forms.Keys keyData)
    {
        if ((keyData & (Forms.Keys.Control | Forms.Keys.Alt)) != 0) return false;
        return _view.HandleNavigation(KeyInterop.KeyFromVirtualKey((int)(keyData & Forms.Keys.KeyCode)));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _commands.Dispose();
        base.Dispose(disposing);
    }

    private sealed class SurfaceRenderer : Forms.ToolStripRenderer
    {
        protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e) => e.Graphics.Clear(Surface);
        protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e) { }
    }

    private sealed class ContentHost(Forms.Control control) : Forms.ToolStripControlHost(control)
    {
        internal void Fill(Rectangle bounds)
        {
            if (Bounds != bounds) SetBounds(bounds);
        }
    }
}
