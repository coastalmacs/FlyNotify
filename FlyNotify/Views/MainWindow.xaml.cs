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

        private DateTime _lastExpertFlyerReminderTime = DateTime.MinValue;

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

        private async void ManualBatchButton_Click(object sender, RoutedEventArgs e)
        {
            // Verify that we have profiles available in memory to process
            if (MonitoredFlights.Count == 0)
            {
                MessageBox.Show(
                    "There are no flight tracking profiles configured to execute. Please add a profile first.",
                    "Batch Processing Ignored",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return;
            }

            // Lock structural commands panel buttons to prevent duplicate thread initialization overlap
            ManualBatchBtn.IsEnabled = false;
            StatusMessageText.Text = "Executing global background flight availability analysis batch query...";
            EngineSchedulerText.Text = "Scheduler Status: Running";

            /*
                Loop sequentially through the flight collection manifest matrix.
                Using 'await' avoids thread starvation issues, ensuring every row item 
                processes gracefully without impacting application interactivity.
            */
            foreach (var profile in MonitoredFlights)
            {
                await FlyNotify.Services.ScraperService.ExecuteScrapeAsync(profile);
            }

            // Flush completed status updates directly down to AppData disk structures
            FlyNotify.Models.FlightProfilePersistence.SaveProfiles(MonitoredFlights);

            // Re-enable core command strip operations
            ManualBatchBtn.IsEnabled = true;
            StatusMessageText.Text = $"Batch Query execution loop completed safely at {DateTime.Now.ToString("HH:mm:ss")}. All profiles synchronized.";
            EngineSchedulerText.Text = "Scheduler Status: Idle";
        }

        /*
            Interactive Data Grid Column Reference Navigation Actions
        */
        /*
                    Interactive Data Grid Column Reference Navigation Actions
                */
        private void QantasLink_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Extract row context data from the clicked FrameworkElement text node
            if (sender is FrameworkElement element && element.DataContext is FlightProfile profile)
            {
                // Delegate query generation directly out to our domain class instance logic
                string targetUrl = profile.BuildQantasQueryUrl();

                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = targetUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Browser Search Initiation Failure]: {ex.Message}");
                }
            }
        }

        private void ExpertFlyerLink_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Parse context data safely from the active cell selection path
            if (sender is FrameworkElement element && element.DataContext is FlightProfile profile)
            {
                // Enforce strict App Specification hourly notification limits
                TimeSpan timeSinceLastPrompt = DateTime.Now - _lastExpertFlyerReminderTime;

                if (timeSinceLastPrompt.TotalHours >= 1.0)
                {
                    MessageBox.Show(
                        "Please ensure you are fully logged into your active ExpertFlyer account in your default web browser before processing availability queries.",
                        "ExpertFlyer Authentication Reminder",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );

                    _lastExpertFlyerReminderTime = DateTime.Now;
                }

                // Compile spec parameters cleanly out via the instance model rules
                string targetUrl = profile.BuildExpertFlyerQueryUrl();

                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = targetUrl,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Browser Search Initiation Failure]: {ex.Message}");
                }
            }
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