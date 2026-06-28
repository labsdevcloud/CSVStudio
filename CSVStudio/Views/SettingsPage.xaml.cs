using CSVStudio.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;

namespace CSVStudio.Views
{
    public sealed partial class SettingsPage : Page
    {
        private const string PreviewLimitKey = "PreviewRowLimit";
        private const string SupportEmail = "support@labsdev.it";

        public SettingsPage()
        {
            this.InitializeComponent();
            LoadCurrentSettings();
        }

        // ─────────────────────────────────────────────────
        // LOAD CURRENT SETTINGS
        // ─────────────────────────────────────────────────
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

        // ─────────────────────────────────────────────────
        // THEME
        // ─────────────────────────────────────────────────
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

        // ─────────────────────────────────────────────────
        // PREVIEW LIMIT
        // ─────────────────────────────────────────────────
        private void PreviewLimitBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (!double.IsNaN(args.NewValue))
                ApplicationData.Current.LocalSettings.Values[PreviewLimitKey] = (int)args.NewValue;
        }

        // ─────────────────────────────────────────────────
        // SEND FEEDBACK (robust mailto handler)
        // ─────────────────────────────────────────────────
        private async void SendFeedback_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mailto = $"mailto:{SupportEmail}" +
                             "?subject=" + Uri.EscapeDataString("CSV Studio - Feedback") +
                             "&body=" + Uri.EscapeDataString(
                                 "Hi LabsDev team,\n\n" +
                                 "[Write your feedback here]\n\n" +
                                 "---\n" +
                                 "CSV Studio v1.0.0");

                var uri = new Uri(mailto);
                var success = await Launcher.LaunchUriAsync(uri);

                if (!success)
                    await ShowNoMailClientDialog();
            }
            catch (Exception)
            {
                await ShowNoMailClientDialog();
            }
        }

        private async Task ShowNoMailClientDialog()
        {
            var dialog = new ContentDialog
            {
                Title = "No email client",
                Content = $"We couldn't open your email client.\n\n" +
                          $"Please send your feedback manually to:\n\n{SupportEmail}",
                PrimaryButtonText = "Copy address",
                CloseButtonText = "Close",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var package = new DataPackage();
                package.SetText(SupportEmail);
                Clipboard.SetContent(package);
            }
        }
    }
}