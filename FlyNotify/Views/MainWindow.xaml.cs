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

            // 1. Recover archived profiles from past application runtimes
            var historicalRecords = FlightProfilePersistence.LoadProfiles();

            // Set up our monitoring array loop and bind directly as items target source
            MonitoredFlights = new ObservableCollection<FlightProfile>(historicalRecords);
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

                    // 2. Commit changes instantly to disk whenever a new row profile is added
                    FlightProfilePersistence.SaveProfiles(MonitoredFlights);
                }
            }
        }

        private void ManualBatchButton_Click(object sender, RoutedEventArgs e)
        {
            /*
                TEMPORARY DEBUGGING HOOK: Inspect the active selection state of the 
                primary DataGrid control to extract a single targeted model instance context.
            */
            if (FlightDataGrid.SelectedItem is FlightProfile selectedProfile)
            {
                // Invoke the URL string compiler logic directly from the data layer object
                string compiledUrl = selectedProfile.BuildQantasQueryUrl();

                try
                {
                    /*
                        Thread-safe assignment to the Win32 Operating System clipboard container.
                        Requires a fallback loop to capture external memory access lock issues.
                    */
                    Clipboard.SetText(compiledUrl);

                    // Update the layout status panel items to give real-time execution feedback
                    StatusMessageText.Text = $"[DEBUG SUCCESS] Qantas URL copied to clipboard for: {selectedProfile.DepartureAirport} -> {selectedProfile.ArrivalAirport}";
                    EngineSchedulerText.Text = "Status: Debug Copied";
                }
                catch (Exception ex)
                {
                    StatusMessageText.Text = $"[DEBUG ERROR] Clipboard allocation failed: {ex.Message}";
                    System.Diagnostics.Debug.WriteLine($"[Clipboard Copy Exception]: {ex.Message}");
                }
            }
            else
            {
                /*
                    Provide visual feedback if the developer clicks the button 
                    without highlighting a row inside the table workspace first.
                */
                MessageBox.Show(
                    "Please highlight a flight profile row inside the table grid first to parse a test URL query string.",
                    "Debug Tool Verification",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
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
            // 3. Flush remaining session data cache blocks natively to local JSON file structures before terminating application process boundary
            FlightProfilePersistence.SaveProfiles(MonitoredFlights);
        }

        private void FlightDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Only execute our interception loop if the physical command target is the Delete key
            if (e.Key == Key.Delete)
            {
                // Verify that there is at least one active selected record highlighting the view
                if (FlightDataGrid.SelectedItems.Count > 0)
                {
                    int totalSelectedCount = FlightDataGrid.SelectedItems.Count;
                    string warningMessage = totalSelectedCount == 1
                        ? "Are you sure you want to permanently delete the selected flight tracking profile?"
                        : $"Are you sure you want to permanently delete all {totalSelectedCount} selected flight tracking profiles?";

                    // Standard systemic message query validation block
                    MessageBoxResult decision = MessageBox.Show(
                        warningMessage,
                        "Confirm Profile Deletion",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    );

                    if (decision == MessageBoxResult.Yes)
                    {
                        /*
                            Because deleting items directly out of an active collection while WPF 
                            loops through SelectedItems causes collection mutation errors, we cache 
                            the targeting elements into a temporary list structure first.
                        */
                        var targetsToRemove = new System.Collections.Generic.List<FlightProfile>();

                        foreach (var selectedItem in FlightDataGrid.SelectedItems)
                        {
                            if (selectedItem is FlightProfile profile)
                            {
                                targetsToRemove.Add(profile);
                            }
                        }

                        // Remove the targeted records directly from the bound ObservableCollection
                        foreach (var target in targetsToRemove)
                        {
                            MonitoredFlights.Remove(target);
                        }

                        // Commit the cleaned data matrix directly to your AppData user configuration file
                        FlightProfilePersistence.SaveProfiles(MonitoredFlights);
                    }
                    else
                    {
                        /*
                            If the user selects 'No', we mark the event routing argument as Handled.
                            This tells WPF to cancel the action completely, protecting the row data.
                        */
                        e.Handled = true;
                    }
                }
            }
        }
    }
}