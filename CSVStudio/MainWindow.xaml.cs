using CSVStudio.Services;
using CSVStudio.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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

            // Sfondo Mica (Windows 11): materiale traslucido moderno.
            // Attivo solo dove supportato; altrimenti resta lo sfondo a tema del Grid.
            if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
            {
                this.SystemBackdrop = new MicaBackdrop();
                if (this.Content is Grid rootGrid)
                    rootGrid.Background = null;   // lascia trasparire la Mica
            }

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