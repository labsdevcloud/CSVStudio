using Microsoft.UI.Xaml;
using Windows.Storage;

namespace CSVStudio.Services
{
    /// <summary>
    /// Gestisce il tema dell'app con persistenza nelle impostazioni locali.
    /// </summary>
    public static class ThemeService
    {
        private const string ThemeKey = "AppTheme";

        public static ElementTheme CurrentTheme { get; private set; } = ElementTheme.Default;

        public static void Initialize(FrameworkElement rootElement)
        {
            var stored = ApplicationData.Current.LocalSettings.Values[ThemeKey] as string;
            CurrentTheme = stored switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
            ApplyTheme(rootElement, CurrentTheme);
        }

        public static void SetTheme(FrameworkElement rootElement, ElementTheme theme)
        {
            CurrentTheme = theme;
            ApplicationData.Current.LocalSettings.Values[ThemeKey] = theme.ToString();
            ApplyTheme(rootElement, theme);
        }

        private static void ApplyTheme(FrameworkElement rootElement, ElementTheme theme)
        {
            if (rootElement != null)
                rootElement.RequestedTheme = theme;
        }
    }
}