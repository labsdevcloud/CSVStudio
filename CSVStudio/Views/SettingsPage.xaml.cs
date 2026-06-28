using CSVStudio.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;

namespace CSVStudio.Views
{
    public sealed partial class SettingsPage : Page
    {
        private const string PreviewLimitKey = "PreviewRowLimit";

        public SettingsPage()
        {
            this.InitializeComponent();
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            // Tema
            ThemeRadios.SelectedIndex = ThemeService.CurrentTheme switch
            {
                ElementTheme.Light => 1,
                ElementTheme.Dark => 2,
                _ => 0
            };

            // Preview limit
            if (ApplicationData.Current.LocalSettings.Values[PreviewLimitKey] is int limit)
                PreviewLimitBox.Value = limit;
        }

        private void ThemeRadios_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (App.MainWindow?.Content is not FrameworkElement root) return;

            var theme = ThemeRadios.SelectedIndex switch
            {
                1 => ElementTheme.Light,
                2 => ElementTheme.Dark,
                _ => ElementTheme.Default
            };

            ThemeService.SetTheme(root, theme);
        }

        private void PreviewLimitBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (!double.IsNaN(args.NewValue))
                ApplicationData.Current.LocalSettings.Values[PreviewLimitKey] = (int)args.NewValue;
        }
    }
}