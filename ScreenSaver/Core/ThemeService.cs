using System.Windows;
using System.Windows.Media;

namespace ScreenSaver.Core;

public sealed class ThemeService
{
    private const string DarkThemeUri  = "/Themes/Theme.Dark.xaml";
    private const string LightThemeUri = "/Themes/Theme.Light.xaml";

    public string CurrentTheme  { get; private set; } = "dark";
    public string CurrentAccent { get; private set; } = "#BF4E16";

    public void Apply(string theme)
    {
        CurrentTheme = theme.ToLowerInvariant();
        var uri = CurrentTheme == "light" ? LightThemeUri : DarkThemeUri;

        var merged = Application.Current.Resources.MergedDictionaries;
        var existing = merged
            .Where(d => d.Source?.OriginalString.Contains("/Themes/Theme.") == true)
            .ToList();
        foreach (var d in existing) merged.Remove(d);
        merged.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) });

        // Ré-applique l'accent : la dict de thème remet AccentBrush à sa valeur par défaut
        ApplyAccent(CurrentAccent);
    }

    public void ApplyAccent(string hex)
    {
        CurrentAccent = hex;
        var color = (Color)ColorConverter.ConvertFromString(hex);
        Application.Current.Resources["AccentBrush"]  = new SolidColorBrush(color);
        Application.Current.Resources["AccentColor"]  = color;
    }
}
