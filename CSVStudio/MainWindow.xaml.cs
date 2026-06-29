using CSVStudio.Services;
using CSVStudio.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace CSVStudio
{
    /// <summary>
    /// Finestra principale dell'applicazione.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            // Icona della finestra (barra del titolo + taskbar)
            // Percorso ASSOLUTO: con il profilo Package la working dir non è quella dell'app
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            AppWindow.SetIcon(iconPath);

            // Inizializza tema persistente
            if (this.Content is FrameworkElement root)
                ThemeService.Initialize(root);

            // Pagina di partenza
            ContentFrame.Navigate(typeof(HomePage));
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                ContentFrame.Navigate(typeof(SettingsPage));
                return;
            }

            if (args.SelectedItemContainer is NavigationViewItem item)
            {
                switch (item.Tag?.ToString())
                {
                    case "home":
                        ContentFrame.Navigate(typeof(HomePage));
                        break;

                    case "batch":
                        ContentFrame.Navigate(typeof(BatchPage));
                        break;
                }
            }
        }
    }
}