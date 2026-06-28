using CSVStudio.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CSVStudio
{
    /// <summary>
    /// Finestra principale dell'applicazione.
    /// Contiene la NavigationView e gestisce la navigazione tra le pagine.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();

            // Pagina di partenza
            ContentFrame.Navigate(typeof(HomePage));
        }

        /// <summary>
        /// Gestisce il cambio di voce nella NavigationView della sidebar.
        /// </summary>
        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                // Future: ContentFrame.Navigate(typeof(SettingsPage));
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
                        // Future step 8: ContentFrame.Navigate(typeof(RecipesPage));
                        break;
                }
            }
        }
    }
}