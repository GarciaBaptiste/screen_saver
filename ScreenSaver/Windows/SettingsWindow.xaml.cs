using System.Windows;
using System.Windows.Controls;
using ScreenSaver.Core;

namespace ScreenSaver.Windows;

public partial class SettingsWindow : Window
{
    private readonly ConfigService _config;
    private readonly ThemeService  _theme;
    private readonly IdleWatcher   _idle;
    private bool _loading;

    public SettingsWindow(ConfigService config, ThemeService theme, IdleWatcher idle)
    {
        _config = config;
        _theme  = theme;
        _idle   = idle;
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        _loading = true;
        var cfg = _config.Config;

        ThresholdSlider.Value = cfg.IdleThresholdSeconds;
        UpdateThresholdLabel(cfg.IdleThresholdSeconds);

        SelectComboByTag(ThemeCombo, cfg.Theme);

        WatermarkCheck.IsChecked  = cfg.Clock.ShowDigitalWatermark;
        OpacitySlider.Value       = cfg.Clock.WatermarkOpacity;
        OpacitySlider.IsEnabled   = cfg.Clock.ShowDigitalWatermark;
        UpdateOpacityLabel(cfg.Clock.WatermarkOpacity);

        MonthGridCheck.IsChecked = cfg.Calendar.ShowMonthGrid;
        SelectComboByTag(FirstDayCombo, cfg.Calendar.FirstDayOfWeek);

        _loading = false;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnThresholdChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => UpdateThresholdLabel((int)e.NewValue);

    private void OnOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => UpdateOpacityLabel(e.NewValue);

    private void OnWatermarkToggle(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        OpacitySlider.IsEnabled = WatermarkCheck.IsChecked == true;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var cfg = _config.Config;

        cfg.IdleThresholdSeconds       = (int)ThresholdSlider.Value;
        cfg.Theme                      = TagOf(ThemeCombo)    ?? "dark";
        cfg.Clock.ShowDigitalWatermark = WatermarkCheck.IsChecked == true;
        cfg.Clock.WatermarkOpacity     = Math.Round(OpacitySlider.Value, 2);
        cfg.Calendar.ShowMonthGrid     = MonthGridCheck.IsChecked == true;
        cfg.Calendar.FirstDayOfWeek    = TagOf(FirstDayCombo) ?? "Monday";

        _config.Save(cfg);
        _idle.ThresholdSeconds = cfg.IdleThresholdSeconds;
        _theme.Apply(cfg.Theme);

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
        => DialogResult = false;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SelectComboByTag(ComboBox combo, string tag)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if ((string)item.Tag == tag) { combo.SelectedItem = item; return; }
        }
        combo.SelectedIndex = 0;
    }

    private static string? TagOf(ComboBox combo)
        => (combo.SelectedItem as ComboBoxItem)?.Tag as string;

    private static string FormatThreshold(int secs) =>
        secs < 60        ? $"{secs} s" :
        secs % 60 == 0   ? $"{secs / 60} min" :
                           $"{secs / 60} min {secs % 60:D2}";

    private void UpdateThresholdLabel(int secs)
    {
        if (ThresholdLabel is null) return;
        ThresholdLabel.Text = FormatThreshold(secs);
    }

    private void UpdateOpacityLabel(double v)
    {
        if (OpacityLabel is null) return;
        OpacityLabel.Text = $"{v:P0}";
    }
}
