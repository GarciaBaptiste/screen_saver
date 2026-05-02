using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Globalization;
using ScreenSaver.Core;

namespace ScreenSaver.Windows;

public class SliderFillConverter : IMultiValueConverter
{
    public object Convert(object[] v, Type t, object p, CultureInfo c)
    {
        if (v.Length < 4) return 0.0;
        double val   = System.Convert.ToDouble(v[0]);
        double min   = System.Convert.ToDouble(v[1]);
        double max   = System.Convert.ToDouble(v[2]);
        double width = System.Convert.ToDouble(v[3]);
        if (max <= min || width <= 0) return 0.0;
        return Math.Max(0, (val - min) / (max - min) * width);
    }
    public object[] ConvertBack(object v, Type[] t, object p, CultureInfo c)
        => throw new NotImplementedException();
}

public partial class SettingsWindow : Window
{
    private readonly ConfigService _config;
    private readonly ThemeService  _theme;
    private readonly IdleWatcher   _idle;
    private readonly string        _originalAccent;

    public SettingsWindow(ConfigService config, ThemeService theme, IdleWatcher idle)
    {
        _config         = config;
        _theme          = theme;
        _idle           = idle;
        _originalAccent = theme.CurrentAccent;
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var cfg = _config.Config;

        ThresholdSlider.Value = cfg.IdleThresholdSeconds;
        UpdateThresholdLabel(cfg.IdleThresholdSeconds);

        SelectThemeSwatch(cfg.Theme);
        SelectAccentSwatch(cfg.AccentColor);

        WatermarkCheck.IsChecked = cfg.Clock.ShowDigitalWatermark;

        MonthGridCheck.IsChecked = cfg.Calendar.ShowMonthGrid;
        foreach (RadioButton rb in FirstDayPanel.Children)
            rb.IsChecked = (string)rb.Tag == cfg.Calendar.FirstDayOfWeek;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnThresholdChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => UpdateThresholdLabel((int)e.NewValue);

    private void OnThemeSwatchChanged(object sender, RoutedEventArgs e) { }

    private void OnAccentChanged(object sender, RoutedEventArgs e)
    {
        var tag = (string)((RadioButton)sender).Tag;
        var hex = tag == "random" ? App.ResolveAccent("random") : tag;
        _theme.ApplyAccent(hex);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var cfg = _config.Config;

        cfg.IdleThresholdSeconds       = (int)ThresholdSlider.Value;
        cfg.Theme                      = SelectedTheme();
        cfg.AccentColor                = SelectedAccentHex();
        cfg.Clock.ShowDigitalWatermark = WatermarkCheck.IsChecked == true;
        cfg.Calendar.ShowMonthGrid     = MonthGridCheck.IsChecked == true;
        cfg.Calendar.FirstDayOfWeek    = FirstDayPanel.Children.OfType<RadioButton>()
                                             .FirstOrDefault(rb => rb.IsChecked == true)
                                             ?.Tag as string ?? "Monday";

        _config.Save(cfg);
        _idle.ThresholdSeconds = cfg.IdleThresholdSeconds;
        _theme.Apply(cfg.Theme);
        _theme.ApplyAccent(App.ResolveAccent(cfg.AccentColor));

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        _theme.ApplyAccent(_originalAccent);
        DialogResult = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SelectThemeSwatch(string theme)
    {
        foreach (RadioButton rb in ThemePanel.Children)
            rb.IsChecked = (string)rb.Tag == theme;
    }

    private string SelectedTheme()
    {
        foreach (RadioButton rb in ThemePanel.Children)
            if (rb.IsChecked == true) return (string)rb.Tag;
        return "dark";
    }

    private void SelectAccentSwatch(string value)
    {
        foreach (RadioButton rb in AccentPanel.Children)
            rb.IsChecked = string.Equals((string)rb.Tag, value, StringComparison.OrdinalIgnoreCase);
    }

    private string SelectedAccentHex()
    {
        foreach (RadioButton rb in AccentPanel.Children)
            if (rb.IsChecked == true) return (string)rb.Tag;
        return "#E93F29";
    }

    private static string FormatThreshold(int secs)
    {
        if (secs < 60)   return $"{secs} s";
        int m = secs / 60, s = secs % 60;
        if (m >= 60)
        {
            int h = m / 60; int rm = m % 60;
            return rm == 0 ? $"{h} h" : $"{h} h {rm:D2}";
        }
        return s == 0 ? $"{m} min" : $"{m} min {s:D2}";
    }

    private void UpdateThresholdLabel(int secs)
    {
        if (ThresholdLabel is null) return;
        ThresholdLabel.Text = FormatThreshold(secs);
    }
}
