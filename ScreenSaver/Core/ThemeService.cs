using System.Windows;
using System.Windows.Media;

namespace ScreenSaver.Core;

public sealed class ThemeService
{
    public string CurrentTheme  { get; private set; } = "dark";
    public string CurrentAccent { get; private set; } = "#E93F29";

    public void Apply(string theme)
    {
        CurrentTheme = theme.ToLowerInvariant();
        var res = Application.Current.Resources;
        bool dark = CurrentTheme == "dark";

        // Lit Black et White depuis Palette.xaml
        var black = (Color)res["Black"];
        var white = (Color)res["White"];

        // Black / White s'inversent selon le thème
        res["BackgroundBrush"]  = B(dark ? black : C(0xE8, 0xE4, 0xDC));
        res["FaceBrush"]        = B(dark ? C(0x25, 0x25, 0x23)  : C(0xFA, 0xFA, 0xF7));
        res["TextPrimaryBrush"] = B(dark ? white : C(0x1C, 0x1C, 0x1A));
        res["TextOnDarkBrush"]  = B(dark ? white : C(0x1C, 0x1C, 0x1A));
        res["HandBrush"]        = B(white);
        res["ClockWhiteBrush"]  = B(dark ? C(0xBA, 0xB7, 0xB0)  : white);
        res["MutedBrush"]       = B(dark ? C(0x9E, 0x9B, 0x94)  : C(0xB0, 0xAD, 0xA6));

        ApplyAccent(CurrentAccent);
    }

    public void ApplyAccent(string hex)
    {
        CurrentAccent = hex;
        var color = (Color)ColorConverter.ConvertFromString(hex);
        Application.Current.Resources["AccentBrush"] = B(color);
        Application.Current.Resources["AccentColor"] = color;
    }

    private static Color           C(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
    private static SolidColorBrush B(Color c)                => new(c);
}
