using System.Windows;

namespace LGMS.Desktop.Common;

public static class ThemeManager
{
    public static string CurrentTheme { get; private set; } = "dark";

    public static void ApplyTheme(string theme)
    {
        var app = Application.Current;
        if (app == null) return;

        var dictUri = theme.Equals("light", StringComparison.OrdinalIgnoreCase)
            ? "Themes/LightTheme.xaml"
            : "Themes/DarkTheme.xaml";

        var newDict = new ResourceDictionary { Source = new Uri(dictUri, UriKind.Relative) };

        // Replace the theme dictionary
        for (int i = 0; i < app.Resources.MergedDictionaries.Count; i++)
        {
            var dict = app.Resources.MergedDictionaries[i];
            if (dict.Source != null && (dict.Source.OriginalString.Contains("DarkTheme") || dict.Source.OriginalString.Contains("LightTheme")))
            {
                app.Resources.MergedDictionaries[i] = newDict;
                CurrentTheme = theme.ToLowerInvariant();
                return;
            }
        }

        app.Resources.MergedDictionaries.Insert(0, newDict);
        CurrentTheme = theme.ToLowerInvariant();
    }

    public static void ToggleTheme()
    {
        ApplyTheme(CurrentTheme == "dark" ? "light" : "dark");
    }
}
