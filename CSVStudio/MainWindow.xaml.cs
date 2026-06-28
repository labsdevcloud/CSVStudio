using CSVStudio.Services;
using CSVStudio.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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

                    case "recipes":
                        // Future Step 8: ContentFrame.Navigate(typeof(RecipesPage));
                        break;
                }
            }
        }
    }
}