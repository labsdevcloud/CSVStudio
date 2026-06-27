using CSVStudio.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CSVStudio
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            ContentFrame.Navigate(typeof(HomePage));
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                // Future: settings page
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
                        // ContentFrame.Navigate(typeof(BatchPage));
                        break;
                    case "recipes":
                        // ContentFrame.Navigate(typeof(RecipesPage));
                        break;
                }
            }
        }
    }
}