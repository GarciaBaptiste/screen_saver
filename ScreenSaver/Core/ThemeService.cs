using System.Windows;

namespace ScreenSaver.Core;

public sealed class ThemeService
{
    private const string DarkThemeUri = "/Themes/Theme.Dark.xaml";
    private const string LightThemeUri = "/Themes/Theme.Light.xaml";

    public string CurrentTheme { get; private set; } = "dark";

    public void Apply(string theme)
    {
        CurrentTheme = theme.ToLowerInvariant();
        var uri = CurrentTheme == "light" ? LightThemeUri : DarkThemeUri;

        var merged = Application.Current.Resources.MergedDictionaries;

        // Remove any existing theme dictionaries
        var existing = merged
            .Where(d => d.Source?.OriginalString.Contains("/Themes/Theme.") == true)
            .ToList();
        foreach (var d in existing)
            merged.Remove(d);

        merged.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) });
    }
}
