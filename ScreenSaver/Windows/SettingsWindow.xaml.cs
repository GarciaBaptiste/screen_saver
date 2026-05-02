using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
    private static readonly double[] OpacityValues = { 0.08, 0.15, 0.28, 0.50 };

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
        var cfg = _config.Config;

        ThresholdSlider.Value = cfg.IdleThresholdSeconds;
        UpdateThresholdLabel(cfg.IdleThresholdSeconds);

        SelectThemeSwatch(cfg.Theme);
        SelectAccentSwatch(cfg.AccentColor);

        SelectOpacityRadio(cfg.Clock.ShowDigitalWatermark, cfg.Clock.WatermarkOpacity);

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
        var color = (System.Windows.Media.Color)
            System.Windows.Media.ColorConverter.ConvertFromString(hex);
        Resources["S.Accent"] = new System.Windows.Media.SolidColorBrush(color);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var cfg = _config.Config;
        var (enabled, opacity) = SelectedOpacity();

        cfg.IdleThresholdSeconds       = (int)ThresholdSlider.Value;
        cfg.Theme                      = SelectedTheme();
        cfg.AccentColor                = SelectedAccentHex();
        cfg.Clock.ShowDigitalWatermark = enabled;
        cfg.Clock.WatermarkOpacity     = opacity;
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
        => DialogResult = false;

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
        return "#BF4E16";
    }

    private void SelectOpacityRadio(bool enabled, double value)
    {
        if (!enabled)
        {
            foreach (RadioButton rb in OpacityPanel.Children)
                rb.IsChecked = (string)rb.Tag == "off";
            return;
        }
        double best = OpacityValues.MinBy(v => Math.Abs(v - value));
        foreach (RadioButton rb in OpacityPanel.Children)
        {
            if (double.TryParse((string)rb.Tag, NumberStyles.Float,
                                CultureInfo.InvariantCulture, out double v))
                rb.IsChecked = Math.Abs(v - best) < 0.001;
        }
    }

    private (bool enabled, double opacity) SelectedOpacity()
    {
        foreach (RadioButton rb in OpacityPanel.Children)
        {
            if (rb.IsChecked != true) continue;
            var tag = (string)rb.Tag;
            if (tag == "off") return (false, 0.15);
            if (double.TryParse(tag, NumberStyles.Float,
                                CultureInfo.InvariantCulture, out double v))
                return (true, v);
        }
        return (true, 0.15);
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
