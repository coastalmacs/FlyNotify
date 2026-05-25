using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using FlyNotify.Models;

namespace FlyNotify.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /*
            Thread-safe internal tracking collection linking runtime profiles 
            directly to the visible layout columns of the DataGrid UI context.
        */
        public ObservableCollection<FlightProfile> MonitoredFlights { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            // Set up our monitoring array loop and bind directly as items target source
            MonitoredFlights = new ObservableCollection<FlightProfile>();
            FlightDataGrid.ItemsSource = MonitoredFlights;
        }

        /*
            UI Interaction Router Event Handlers
        */
        private void AddFlightButton_Click(object sender, RoutedEventArgs e)
        {
            AddFlightDialog dialog = new AddFlightDialog();
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                // Pull out the completed modal data contract and pipe it cleanly into the active table grid
                if (dialog.TargetProfile != null)
                {
                    MonitoredFlights.Add(dialog.TargetProfile);
                }
            }
        }

        private void ManualBatchButton_Click(object sender, RoutedEventArgs e)
        {
            // Commented Placeholder: Instantly trigger an asynchronous network scrape batch evaluation
        }

        /*
            Interactive Data Grid Column Reference Navigation Actions
        */
        private void QantasLink_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Commented Placeholder: Extract data model row context and open target Qantas Reward Finder URL string
        }

        private void ExpertFlyerLink_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Commented Placeholder: Extract data model row context and open target ExpertFlyer availability query string
        }

        /*
            Application Workspace Shell Window Lifecycle Interceptions
        */
        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            // Commented Placeholder: Handle window state transitions to/from system notification tray
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            // Commented Placeholder: Safe write uncommitted data configurations locally to JSON files prior to teardown
        }
    }
}