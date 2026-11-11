using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using Button = System.Windows.Controls.Button;
using UserControl = System.Windows.Controls.UserControl;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using CheckBox = System.Windows.Controls.CheckBox;

namespace Radiomat;

public partial class TrayMenuView : UserControl
{
    private readonly List<ButtonBase> _buttons = [];
    private bool _synchronizing;
    internal event Action<double>? VolumeChanged;
    internal event Action<Forms.ToolStripItem>? CommandInvoked;
    internal event Action? CloseRequested;

    public TrayMenuView()
    {
        InitializeComponent();
        DataContext = Strings.Current;
    }

    internal void SetVolume(double value)
    {
        _synchronizing = true;
        try { VolumeSlider.Value = Math.Clamp(value, 0, 100); UpdatePercent(); }
        finally { _synchronizing = false; }
    }

    private void UpdatePercent()
    {
        VolumePercent.Text = $"{VolumeSlider.Value:0}%";
        System.Windows.Automation.AutomationProperties.SetHelpText(VolumeSlider, VolumePercent.Text);
    }

    private void SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // XAML initialization can raise ValueChanged before named fields exist.
        if (VolumePercent == null) return;
        UpdatePercent();
        if (!_synchronizing) VolumeChanged?.Invoke(e.NewValue);
    }

    internal void SetCommands(IEnumerable<Forms.ToolStripItem> items)
    {
        CommandsPanel.Children.Clear();
        _buttons.Clear();
        foreach (var item in items.Where(i => i.Available))
        {
            if (item is Forms.ToolStripSeparator)
            {
                CommandsPanel.Children.Add(new Border
                {
                    Height = 1, Margin = new Thickness(8, 4, 8, 4),
                    Background = new SolidColorBrush(Color.FromRgb(215, 223, 238))
                });
                continue;
            }
            bool selected = item is Forms.ToolStripMenuItem { Checked: true };
            var label = new TextBlock
            {
                Text = (item.Text ?? "").Replace("&&", "&"), TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal
            };
            ButtonBase command;
            if (item is Forms.ToolStripMenuItem { CheckOnClick: true })
            {
                command = new CheckBox
                {
                    Content = label, IsChecked = selected, Style = (Style)FindResource("MenuToggle")
                };
            }
            else
            {
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
                row.ColumnDefinitions.Add(new ColumnDefinition());
                if (selected)
                    row.Children.Add(new TextBlock { Text = "✓", Foreground = new SolidColorBrush(Color.FromRgb(48, 87, 175)),
                        VerticalAlignment = VerticalAlignment.Center });
                Grid.SetColumn(label, 1);
                row.Children.Add(label);
                var button = new Button { Content = row, Style = (Style)FindResource("MenuCommand") };
                if (selected) button.Background = new SolidColorBrush(Color.FromRgb(229, 234, 243));
                command = button;
            }
            command.IsEnabled = item.Enabled;
            command.ToolTip = string.IsNullOrWhiteSpace(item.ToolTipText) ? label.Text : item.ToolTipText;
            command.HorizontalAlignment = HorizontalAlignment.Stretch;
            System.Windows.Automation.AutomationProperties.SetName(command, label.Text);
            command.Click += (_, _) => CommandInvoked?.Invoke(item);
            CommandsPanel.Children.Add(command);
            _buttons.Add(command);
        }
    }

    internal void FocusFirstCommand() => _buttons.FirstOrDefault(b => b.IsEnabled)?.Focus();

    private void MenuKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == 0)
            e.Handled = HandleNavigation(e.Key);
    }

    internal bool HandleNavigation(Key key)
    {
        if (key == Key.Escape) { CloseRequested?.Invoke(); return true; }
        if (key == Key.Tab)
        {
            if (VolumeSlider.IsKeyboardFocusWithin) FocusFirstCommand();
            else VolumeSlider.Focus();
            return true;
        }
        if (VolumeSlider.IsKeyboardFocusWithin)
        {
            double? value = key switch
            {
                Key.Up or Key.Right => VolumeSlider.Value + 1,
                Key.Down or Key.Left => VolumeSlider.Value - 1,
                Key.PageUp => VolumeSlider.Value + 5,
                Key.PageDown => VolumeSlider.Value - 5,
                Key.Home => 0, Key.End => 100, _ => null
            };
            if (!value.HasValue) return false;
            VolumeSlider.Value = Math.Clamp(value.Value, 0, 100);
            return true;
        }
        if (key is Key.Up or Key.Down)
        {
            var enabled = _buttons.Where(b => b.IsEnabled).ToArray();
            if (enabled.Length == 0) return false;
            int index = Array.FindIndex(enabled, b => b.IsKeyboardFocusWithin);
            index = index < 0 ? 0 : (index + (key == Key.Up ? -1 : 1) + enabled.Length) % enabled.Length;
            enabled[index].Focus();
            return true;
        }
        return false;
    }
}
