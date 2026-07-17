using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Controls;
using FlyNotify.Models;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;
using MenuItem = System.Windows.Controls.MenuItem;
using ContextMenu = System.Windows.Controls.ContextMenu;
using Clipboard = System.Windows.Clipboard;

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
        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private bool _isExitingFromTray;
        private readonly System.Threading.SemaphoreSlim _scrapeSemaphore = new(1, 1);
        private System.Threading.CancellationTokenSource? _schedulerCts;

        public MainWindow()
        {
            InitializeComponent();

            // Hook up window lifecycle event handlers
            this.Closing += MainWindow_Closing;
            this.StateChanged += MainWindow_StateChanged;

            // 1. Recover archived profiles from past application runtimes
            var historicalRecords = FlightProfilePersistence.LoadProfiles();

            // Delete any profiles whose travel dates are in the past
            int deletedCount = historicalRecords.RemoveAll(p => p.TravelDate.Date < DateTime.Today);
            if (deletedCount > 0)
            {
                FlightProfilePersistence.SaveProfiles(historicalRecords);
            }

            // Set up our monitoring array loop and bind directly as items target source
            MonitoredFlights = new ObservableCollection<FlightProfile>(historicalRecords);
            FlightDataGrid.ItemsSource = MonitoredFlights;

            // 2. Initialize the system tray notification icon
            InitializeNotifyIcon();

            // Initialize and run the daily automatic scheduler
            _schedulerCts = new System.Threading.CancellationTokenSource();
            StartDailyScheduler(_schedulerCts.Token);

            // Update bottom status bar if expired profiles were cleaned
            if (deletedCount > 0)
            {
                StatusMessageText.Text = $"System Ready. Removed {deletedCount} expired flight profile(s) on startup.";
            }
        }

        /*
            UI Interaction Router Event Handlers
        */
        private void AddFlightButton_Click(object sender, RoutedEventArgs e)
        {
            FlightProfileDialog dialog = new()
            {
                Owner = this
            };

            if (dialog.ShowDialog() is true)
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

            MessageBoxResult chosenResult = MessageBox.Show(
                "Which type of query would you like to run?\nClick Yes to execute a live query, no to run local test query or Cancel to abort.",
                "Batch Query Mode",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question
            );

            if (chosenResult == MessageBoxResult.Cancel)
            {
                return;
            }

            bool isLive = chosenResult == MessageBoxResult.Yes;

            // Acquire lock to prevent overlapping runs
            await _scrapeSemaphore.WaitAsync();
            try
            {
                var snapshot = GetMonitoredFlightsSnapshot();

                FlyNotify.Services.ScraperService.UseMockData = !isLive;

                ManualBatchBtn.IsEnabled = false;
                StatusMessageText.Text = "Executing global background flight availability analysis batch query...";
                EngineSchedulerText.Text = "Scheduler Status: Running";

                var profilesToQuery = MonitoredFlights
                    .OrderByDescending(p => p.IsWildcardOrRegion)
                    .ToList();
                var random = new Random();
                int liveQueriesScraped = 0;

                for (int i = 0; i < profilesToQuery.Count; i++)
                {
                    var profile = profilesToQuery[i];

                    // Skip network checks if covered by a wildcard search
                    if (IsProfileCoveredByWildcard(profile, profilesToQuery))
                    {
                        StatusMessageText.Text = $"Skipping check for {profile.DepartureAirport} -> {profile.ArrivalAirport} (covered by ALL query)...";
                        continue;
                    }

                    /*
                        Apply random human-like delay between requests if querying live servers.
                    */
                    if (liveQueriesScraped > 0 && isLive)
                    {
                        int delayMs = random.Next(2000, 5000);
                        StatusMessageText.Text = $"Waiting {delayMs / 1000.0:F1}s before next query to mimic human browsing behavior...";
                        await System.Threading.Tasks.Task.Delay(delayMs);
                    }

                    if (isLive)
                    {
                        liveQueriesScraped++;
                    }

                    StatusMessageText.Text = $"Scraping availability for route {profile.DepartureAirport} -> {profile.ArrivalAirport}...";

                    List<FlightProfile> results;
                    try
                    {
                        results = await FlyNotify.Services.ScraperService.ExecuteScrapeAsync(profile, msg =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                StatusMessageText.Text = $"[{profile.DepartureAirport} -> {profile.ArrivalAirport}] {msg}";
                            });
                        });
                    }
                    catch (Exception ex)
                    {
                        StatusMessageText.Text = $"Batch query aborted due to error on route {profile.DepartureAirport} -> {profile.ArrivalAirport}: {ex.Message}";
                        break;
                    }

                    if (profile.IsWildcardOrRegion)
                    {
                        foreach (var result in results)
                        {
                            string seatsDetail = result.AvailabilityStatus;
                            result.DetailedStatus = seatsDetail;
                            result.AvailabilityStatus = "Available";

                            var existing = MonitoredFlights.FirstOrDefault(p =>
                                p.DepartureAirport.Equals(result.DepartureAirport, StringComparison.OrdinalIgnoreCase) &&
                                p.ArrivalAirport.Equals(result.ArrivalAirport, StringComparison.OrdinalIgnoreCase) &&
                                p.TravelDate.Date == result.TravelDate.Date &&
                                p.PassengerCount == result.PassengerCount &&
                                p.SelectedCabins == result.SelectedCabins &&
                                (p.FlightNumber.Equals(result.FlightNumber, StringComparison.OrdinalIgnoreCase) || p.FlightNumber == "TBD"));

                            if (existing != null)
                            {
                                existing.FlightNumber = result.FlightNumber;
                                existing.DepartureTime = result.DepartureTime;
                                existing.ArrivalTime = result.ArrivalTime;
                                existing.Duration = result.Duration;
                                existing.AvailabilityStatus = "Available";
                                existing.DetailedStatus = seatsDetail;
                                existing.LastChecked = DateTime.Now;
                            }
                            else
                            {
                                MonitoredFlights.Add(result);
                            }
                        }

                        // Synchronize covered specific profiles that might not be in the results
                        var coveredSpecifics = MonitoredFlights.Where(p => IsProfileCoveredByWildcard(p, new[] { profile })).ToList();
                        foreach (var specific in coveredSpecifics)
                        {
                            var match = results.FirstOrDefault(r =>
                                r.ArrivalAirport.Equals(specific.ArrivalAirport, StringComparison.OrdinalIgnoreCase) &&
                                (specific.FlightNumber == "TBD" || r.FlightNumber.Equals(specific.FlightNumber, StringComparison.OrdinalIgnoreCase)));

                            if (match != null)
                            {
                                specific.FlightNumber = match.FlightNumber;
                                specific.DepartureTime = match.DepartureTime;
                                specific.ArrivalTime = match.ArrivalTime;
                                specific.Duration = match.Duration;
                                specific.AvailabilityStatus = "Available";
                                specific.DetailedStatus = match.DetailedStatus;
                                specific.LastChecked = DateTime.Now;
                            }
                            else
                            {
                                specific.AvailabilityStatus = "Checked";
                                specific.DetailedStatus = "No Classes Found";
                                specific.FlightNumber = "TBD";
                                specific.DepartureTime = "TBD";
                                specific.ArrivalTime = "TBD";
                                specific.Duration = "TBD";
                                specific.LastChecked = DateTime.Now;
                            }
                        }

                        if (results.Count > 0)
                        {
                            profile.AvailabilityStatus = "Available";
                            profile.DetailedStatus = string.Join(" | ", results.Select(r => $"{r.ArrivalAirport}: {r.DetailedStatus}"));
                            profile.FlightNumber = "TBD";
                            profile.DepartureTime = "TBD";
                            profile.ArrivalTime = "TBD";
                            profile.Duration = "TBD";
                            profile.LastChecked = DateTime.Now;
                        }
                        else
                        {
                            profile.AvailabilityStatus = "Checked";
                            profile.DetailedStatus = "No Classes Found";
                            profile.FlightNumber = "TBD";
                            profile.DepartureTime = "TBD";
                            profile.ArrivalTime = "TBD";
                            profile.Duration = "TBD";
                            profile.LastChecked = DateTime.Now;
                        }
                    }
                    else
                    {
                        if (results.Count > 0)
                        {
                            var firstMatch = results[0];
                            profile.FlightNumber = firstMatch.FlightNumber;
                            profile.DepartureTime = firstMatch.DepartureTime;
                            profile.ArrivalTime = firstMatch.ArrivalTime;
                            profile.Duration = firstMatch.Duration;
                            profile.AvailabilityStatus = "Available";
                            profile.DetailedStatus = firstMatch.AvailabilityStatus;
                            profile.LastChecked = DateTime.Now;
                        }
                        else
                        {
                            profile.AvailabilityStatus = "Checked";
                            profile.DetailedStatus = "No Classes Found";
                            profile.FlightNumber = "TBD";
                            profile.DepartureTime = "TBD";
                            profile.ArrivalTime = "TBD";
                            profile.Duration = "TBD";
                            profile.LastChecked = DateTime.Now;
                        }
                    }

                    string detail = results.Count > 0 ? (profile.IsWildcardOrRegion ? string.Join(" | ", results.Select(r => $"{r.ArrivalAirport}: {r.DetailedStatus}")) : profile.DetailedStatus) : "No Classes Found";
                    StatusMessageText.Text = $"Route {profile.DepartureAirport} -> {profile.ArrivalAirport}: {profile.AvailabilityStatus} ({detail})";
                }

                FlyNotify.Models.FlightProfilePersistence.SaveProfiles(MonitoredFlights);
                StatusMessageText.Text = $"Batch Query execution loop completed safely at {DateTime.Now:HH:mm:ss}. All profiles synchronized.";

                await CompareAndSendNotificationAsync(snapshot);
            }
            finally
            {
                _scrapeSemaphore.Release();
                ManualBatchBtn.IsEnabled = true;

                // Re-calculate and display scheduler next run time in local time
                DateTime nextRunUtc = DateTime.UtcNow.Date.AddDays(1);
                EngineSchedulerText.Text = $"Scheduler Status: Idle (Next run: {nextRunUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss})";
            }
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
        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                this.Hide();
            }
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (_isExitingFromTray)
            {
                _schedulerCts?.Cancel();
                _schedulerCts?.Dispose();
                FlightProfilePersistence.SaveProfiles(MonitoredFlights);
                return;
            }

            MessageBoxResult result = MessageBox.Show(
                "FlyNotify needs to remain running to send email notifications. Would you like to close FlyNotify or minimise to the notification area?\n\nClick Yes to close, No to minimise the application, or Cancel to keep the window open.",
                "Exit FlyNotify",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                _schedulerCts?.Cancel();
                _schedulerCts?.Dispose();
                CleanupNotifyIcon();
                FlightProfilePersistence.SaveProfiles(MonitoredFlights);
            }
            else if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
            }
            else
            {
                e.Cancel = true;
                this.Hide();
            }
        }

        /*
            Background loop that periodically schedules execution of midnight UTC batch checks.
        */
        private void StartDailyScheduler(System.Threading.CancellationToken token)
        {
            System.Threading.Tasks.Task.Run(async () =>
            {
                // Calculate initial next run time (10am local / midnight UTC of the next day)
                DateTime nextRunUtc = DateTime.UtcNow.Date.AddDays(1);
                DateTime nextRunLocal = nextRunUtc.ToLocalTime();

                Dispatcher.Invoke(() =>
                {
                    EngineSchedulerText.Text = $"Scheduler Status: Idle (Next run: {nextRunLocal:yyyy-MM-dd HH:mm:ss})";
                });

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        // Check the time every 10 seconds
                        await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(10), token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    if (DateTime.UtcNow >= nextRunUtc)
                    {
                        // Check if we are running late (e.g. system was hibernated/asleep)
                        if (DateTime.UtcNow - nextRunUtc > TimeSpan.FromMinutes(1))
                        {
                            Dispatcher.Invoke(() =>
                            {
                                StatusMessageText.Text = "System resumed. Waiting 20 seconds for network connections to stabilize...";
                            });
                            try
                            {
                                await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(20), token);
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }
                        }

                        await RunAutomatedBatchQueryAsync(token);

                        nextRunUtc = DateTime.UtcNow.Date.AddDays(1);
                        DateTime updatedNextRunLocal = nextRunUtc.ToLocalTime();
                        Dispatcher.Invoke(() =>
                        {
                            EngineSchedulerText.Text = $"Scheduler Status: Idle (Next run: {updatedNextRunLocal:yyyy-MM-dd HH:mm:ss})";
                        });
                    }
                }
            }, token);
        }

        /*
            Executes automated daily live flight checks asynchronously and thread-safely.
        */
        private async System.Threading.Tasks.Task RunAutomatedBatchQueryAsync(System.Threading.CancellationToken token)
        {
            try
            {
                await _scrapeSemaphore.WaitAsync(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                var snapshot = GetMonitoredFlightsSnapshot();

                Dispatcher.Invoke(() =>
                {
                    ManualBatchBtn.IsEnabled = false;
                    EngineSchedulerText.Text = "Scheduler Status: Running";
                    StatusMessageText.Text = "Automated daily midnight UTC batch query execution started...";
                });

                // Automated runs must always use live scraping
                FlyNotify.Services.ScraperService.UseMockData = false;

                System.Collections.Generic.List<FlightProfile> profilesToQuery;
                lock (MonitoredFlights)
                {
                    profilesToQuery = MonitoredFlights
                        .OrderByDescending(p => p.IsWildcardOrRegion)
                        .ToList();
                }

                var random = new Random();
                int liveQueriesScraped = 0;

                for (int i = 0; i < profilesToQuery.Count; i++)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    var profile = profilesToQuery[i];

                    bool isCovered = false;
                    Dispatcher.Invoke(() =>
                    {
                        isCovered = IsProfileCoveredByWildcard(profile, profilesToQuery);
                    });

                    if (isCovered)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            StatusMessageText.Text = $"Automated query: skipping {profile.DepartureAirport} -> {profile.ArrivalAirport} (covered by ALL query)...";
                        });
                        continue;
                    }

                    // Spacing delay to avoid bot appearance
                    if (liveQueriesScraped > 0)
                    {
                        int delayMs = random.Next(2000, 5000);
                        Dispatcher.Invoke(() =>
                        {
                            StatusMessageText.Text = $"Automated query: waiting {delayMs / 1000.0:F1}s to mimic human browsing...";
                        });

                        try
                        {
                            await System.Threading.Tasks.Task.Delay(delayMs, token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }

                    liveQueriesScraped++;

                    Dispatcher.Invoke(() =>
                    {
                        StatusMessageText.Text = $"Automated query: scraping {profile.DepartureAirport} -> {profile.ArrivalAirport}...";
                    });

                    List<FlightProfile> results;
                    try
                    {
                        results = await FlyNotify.Services.ScraperService.ExecuteScrapeAsync(profile, msg =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                StatusMessageText.Text = $"Automated query: [{profile.DepartureAirport} -> {profile.ArrivalAirport}] {msg}";
                            });
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            StatusMessageText.Text = $"Automated query aborted due to error on route {profile.DepartureAirport} -> {profile.ArrivalAirport}: {ex.Message}";
                        });
                        break;
                    }

                    Dispatcher.Invoke(() =>
                    {
                        if (profile.IsWildcardOrRegion)
                        {
                            foreach (var result in results)
                            {
                                string seatsDetail = result.AvailabilityStatus;
                                result.DetailedStatus = seatsDetail;
                                result.AvailabilityStatus = "Available";

                                var existing = MonitoredFlights.FirstOrDefault(p =>
                                    p.DepartureAirport.Equals(result.DepartureAirport, StringComparison.OrdinalIgnoreCase) &&
                                    p.ArrivalAirport.Equals(result.ArrivalAirport, StringComparison.OrdinalIgnoreCase) &&
                                    p.TravelDate.Date == result.TravelDate.Date &&
                                    p.PassengerCount == result.PassengerCount &&
                                    p.SelectedCabins == result.SelectedCabins &&
                                    (p.FlightNumber.Equals(result.FlightNumber, StringComparison.OrdinalIgnoreCase) || p.FlightNumber == "TBD"));

                                if (existing != null)
                                {
                                    existing.FlightNumber = result.FlightNumber;
                                    existing.DepartureTime = result.DepartureTime;
                                    existing.ArrivalTime = result.ArrivalTime;
                                    existing.Duration = result.Duration;
                                    existing.AvailabilityStatus = "Available";
                                    existing.DetailedStatus = seatsDetail;
                                    existing.LastChecked = DateTime.Now;
                                }
                                else
                                {
                                    MonitoredFlights.Add(result);
                                }
                            }

                            // Synchronize covered specific profiles that might not be in the results
                            var coveredSpecifics = MonitoredFlights.Where(p => IsProfileCoveredByWildcard(p, new[] { profile })).ToList();
                            foreach (var specific in coveredSpecifics)
                            {
                                var match = results.FirstOrDefault(r =>
                                    r.ArrivalAirport.Equals(specific.ArrivalAirport, StringComparison.OrdinalIgnoreCase) &&
                                    (specific.FlightNumber == "TBD" || r.FlightNumber.Equals(specific.FlightNumber, StringComparison.OrdinalIgnoreCase)));

                                if (match != null)
                                {
                                    specific.FlightNumber = match.FlightNumber;
                                    specific.DepartureTime = match.DepartureTime;
                                    specific.ArrivalTime = match.ArrivalTime;
                                    specific.Duration = match.Duration;
                                    specific.AvailabilityStatus = "Available";
                                    specific.DetailedStatus = match.DetailedStatus;
                                    specific.LastChecked = DateTime.Now;
                                }
                                else
                                {
                                    specific.AvailabilityStatus = "Checked";
                                    specific.DetailedStatus = "No Classes Found";
                                    specific.FlightNumber = "TBD";
                                    specific.DepartureTime = "TBD";
                                    specific.ArrivalTime = "TBD";
                                    specific.Duration = "TBD";
                                    specific.LastChecked = DateTime.Now;
                                }
                            }

                            if (results.Count > 0)
                            {
                                profile.AvailabilityStatus = "Available";
                                profile.DetailedStatus = string.Join(" | ", results.Select(r => $"{r.ArrivalAirport}: {r.DetailedStatus}"));
                                profile.FlightNumber = "TBD";
                                profile.DepartureTime = "TBD";
                                profile.ArrivalTime = "TBD";
                                profile.Duration = "TBD";
                                profile.LastChecked = DateTime.Now;
                            }
                            else
                            {
                                profile.AvailabilityStatus = "Checked";
                                profile.DetailedStatus = "No Classes Found";
                                profile.FlightNumber = "TBD";
                                profile.DepartureTime = "TBD";
                                profile.ArrivalTime = "TBD";
                                profile.Duration = "TBD";
                                profile.LastChecked = DateTime.Now;
                            }
                        }
                        else
                        {
                            if (results.Count > 0)
                            {
                                var firstMatch = results[0];
                                profile.FlightNumber = firstMatch.FlightNumber;
                                profile.DepartureTime = firstMatch.DepartureTime;
                                profile.ArrivalTime = firstMatch.ArrivalTime;
                                profile.Duration = firstMatch.Duration;
                                profile.AvailabilityStatus = "Available";
                                profile.DetailedStatus = firstMatch.AvailabilityStatus;
                                profile.LastChecked = DateTime.Now;
                            }
                            else
                            {
                                profile.AvailabilityStatus = "Checked";
                                profile.DetailedStatus = "No Classes Found";
                                profile.FlightNumber = "TBD";
                                profile.DepartureTime = "TBD";
                                profile.ArrivalTime = "TBD";
                                profile.Duration = "TBD";
                                profile.LastChecked = DateTime.Now;
                            }
                        }

                        string detail = results.Count > 0 ? (profile.IsWildcardOrRegion ? string.Join(" | ", results.Select(r => $"{r.ArrivalAirport}: {r.DetailedStatus}")) : profile.DetailedStatus) : "No Classes Found";
                        StatusMessageText.Text = $"Automated query: route {profile.DepartureAirport} -> {profile.ArrivalAirport} is {profile.AvailabilityStatus} ({detail})";
                    });
                }

                Dispatcher.Invoke(() =>
                {
                    FlyNotify.Models.FlightProfilePersistence.SaveProfiles(MonitoredFlights);
                    StatusMessageText.Text = $"Automated batch query completed at {DateTime.Now:HH:mm:ss}. Profiles saved.";
                });

                await CompareAndSendNotificationAsync(snapshot);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Automated Scheduler Execution Failure]: {ex.Message}");
            }
            finally
            {
                _scrapeSemaphore.Release();
                Dispatcher.Invoke(() =>
                {
                    ManualBatchBtn.IsEnabled = true;
                });
            }
        }

        /*
            Initializes the system tray notification area icon and hooks up right-click menus.
        */
        private void InitializeNotifyIcon()
        {
            try
            {
                _notifyIcon = new System.Windows.Forms.NotifyIcon();
                _notifyIcon.Text = "FlyNotify";

                // Load app icon from the embedded application pack resource
                var iconUri = new Uri("pack://application:,,,/Assets/app.ico");
                var resourceStream = Application.GetResourceStream(iconUri);
                if (resourceStream != null)
                {
                    _notifyIcon.Icon = new System.Drawing.Icon(resourceStream.Stream);
                }

                _notifyIcon.Visible = true;

                // Double click restores the main window
                _notifyIcon.DoubleClick += (sender, args) =>
                {
                    RestoreWindow();
                };

                // Build context menu strip using Windows Forms APIs
                var contextMenu = new System.Windows.Forms.ContextMenuStrip();

                var restoreItem = new System.Windows.Forms.ToolStripMenuItem("Restore");
                restoreItem.Click += (sender, args) =>
                {
                    RestoreWindow();
                };
                contextMenu.Items.Add(restoreItem);

                var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
                exitItem.Click += (sender, args) =>
                {
                    _isExitingFromTray = true;
                    CleanupNotifyIcon();
                    Application.Current.Shutdown();
                };
                contextMenu.Items.Add(exitItem);

                _notifyIcon.ContextMenuStrip = contextMenu;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotifyIcon Init Failure]: {ex.Message}");
            }
        }

        /*
            Restores and focuses the main window on screen.
        */
        private void RestoreWindow()
        {
            this.Show();
            if (this.WindowState == WindowState.Minimized)
            {
                this.WindowState = WindowState.Normal;
            }
            this.Activate();
        }

        /*
            Safely cleans up the notify icon before app disposal.
        */
        private void CleanupNotifyIcon()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }

        /*
            Updates the bottom status message when the user selects a different flight profile in the grid.
        */
        private void FlightDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FlightDataGrid.SelectedItem is FlightProfile profile)
            {
                if (profile.AvailabilityStatus == "Available" || profile.AvailabilityStatus == "Checked")
                {
                    StatusMessageText.Text = $"Selected profile {profile.DepartureAirport} -> {profile.ArrivalAirport} | Status: {profile.AvailabilityStatus} ({profile.DetailedStatus})";
                }
                else
                {
                    StatusMessageText.Text = $"Selected profile {profile.DepartureAirport} -> {profile.ArrivalAirport} | Status: {profile.AvailabilityStatus}";
                }
            }
        }

        private void FlightDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            var column = e.Column;
            e.Handled = true;

            var sortDirection = column.SortDirection == ListSortDirection.Ascending 
                ? ListSortDirection.Descending 
                : ListSortDirection.Ascending;

            column.SortDirection = sortDirection;

            string sortMember = column.SortMemberPath;
            if (string.IsNullOrEmpty(sortMember) && column is DataGridBoundColumn boundColumn)
            {
                if (boundColumn.Binding is System.Windows.Data.Binding binding)
                {
                    sortMember = binding.Path.Path;
                }
            }

            if (sortMember == "FullScheduleDisplay") sortMember = "TravelDate";
            if (sortMember == "LastCheckedDisplay") sortMember = "LastChecked";

            if (string.IsNullOrEmpty(sortMember)) return;

            var view = CollectionViewSource.GetDefaultView(FlightDataGrid.ItemsSource);
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(sortMember, sortDirection));
        }

        private void FlightDataGrid_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Only execute our interception loop if the physical command target is the Delete key
            if (e.Key == Key.Delete)
            {
                // Verify that there is at least one active selected record highlighting the view
                if (FlightDataGrid.SelectedItems.Count > 0)
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
            }
        }

        /*
            Helper method to show the FlightProfileDialog and update the edited profile.
        */
        private void EditProfile(FlightProfile profile)
        {
            FlightProfileDialog dialog = new(profile)
            {
                Owner = this
            };

            if (dialog.ShowDialog() is true && dialog.TargetProfile != null)
            {
                int index = MonitoredFlights.IndexOf(profile);
                if (index >= 0)
                {
                    MonitoredFlights[index] = dialog.TargetProfile;
                    FlightProfilePersistence.SaveProfiles(MonitoredFlights);
                }
            }
        }

        private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // If the user double-clicked on interactive elements like the links, avoid triggering edit
            if (e.OriginalSource is DependencyObject originalSource)
            {
                if (originalSource is TextBlock tb && (tb.Text == "Qantas" || tb.Text == "ExpertFlyer" || tb.Text == "|"))
                {
                    return;
                }
            }

            if (sender is DataGridRow row && row.DataContext is FlightProfile profile)
            {
                EditProfile(profile);
            }
        }

        /*
            Right-click Context Menu Event Handlers
        */
        private void EditProfile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is FlightProfile profile)
            {
                EditProfile(profile);
            }
        }

        /*
            Removes all currently highlighted profile definitions from the tracking grid.
        */
        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (FlightDataGrid.SelectedItems.Count > 0)
            {
                var targetsToRemove = new System.Collections.Generic.List<FlightProfile>();

                foreach (var selectedItem in FlightDataGrid.SelectedItems)
                {
                    if (selectedItem is FlightProfile profile)
                    {
                        targetsToRemove.Add(profile);
                    }
                }

                foreach (var target in targetsToRemove)
                {
                    MonitoredFlights.Remove(target);
                }

                FlightProfilePersistence.SaveProfiles(MonitoredFlights);
            }
        }

        /*
            Dynamically configures menu item state depending on row selection volume.
        */
        private void RowContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu menu)
            {
                int selectionCount = FlightDataGrid.SelectedItems.Count;

                var editMenuItem = menu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Header.ToString() == "Edit Profile");
                if (editMenuItem != null)
                {
                    editMenuItem.IsEnabled = selectionCount == 1;
                }

                var copyQfMenuItem = menu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Header.ToString() == "Copy QF query to clipboard");
                if (copyQfMenuItem != null)
                {
                    copyQfMenuItem.IsEnabled = selectionCount == 1;
                }

                var copyEfMenuItem = menu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Header.ToString() == "Copy EF query to clipboard");
                if (copyEfMenuItem != null)
                {
                    copyEfMenuItem.IsEnabled = selectionCount == 1;
                }
            }
        }

        /*
            Copies the generated Qantas search URL for the selected flight profile to the clipboard.
        */
        private void CopyQfQuery_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is FlightProfile profile)
            {
                try
                {
                    Clipboard.SetText(profile.BuildQantasQueryUrl());
                    StatusMessageText.Text = "Qantas query URL successfully copied to clipboard.";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Clipboard Copy Failure]: {ex.Message}");
                }
            }
        }

        /*
            Copies the generated ExpertFlyer search URL for the selected flight profile to the clipboard.
        */
        private void CopyEfQuery_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is FlightProfile profile)
            {
                try
                {
                    Clipboard.SetText(profile.BuildExpertFlyerQueryUrl());
                    StatusMessageText.Text = "ExpertFlyer query URL successfully copied to clipboard.";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Clipboard Copy Failure]: {ex.Message}");
                }
            }
        }

        /*
            Generates a unique tracking key for a flight profile to identify specific route, 
            date, flight number, passenger count, and cabin configuration combinations.
        */
        private string GetProfileKey(FlightProfile profile)
        {
            return $"{profile.DepartureAirport.ToUpper()}-{profile.ArrivalAirport.ToUpper()}-{profile.TravelDate:yyyyMMdd}-{profile.FlightNumber.ToUpper()}-{profile.PassengerCount}-{profile.CabinClass}";
        }

        /*
            Determines if a specific flight profile is logically covered by any active wildcard "ALL" query
            in the currently running batch.
        */
        private bool IsProfileCoveredByWildcard(FlightProfile specific, System.Collections.Generic.IEnumerable<FlightProfile> activeProfiles)
        {
            if (specific.IsWildcardOrRegion)
            {
                return false;
            }

            return activeProfiles.Any(allProfile =>
                allProfile.IsWildcardOrRegion &&
                allProfile.DepartureAirport.Equals(specific.DepartureAirport, StringComparison.OrdinalIgnoreCase) &&
                allProfile.TravelDate.Date == specific.TravelDate.Date &&
                allProfile.TravelEndDate.Date == specific.TravelEndDate.Date &&
                allProfile.PassengerCount == specific.PassengerCount &&
                (specific.SelectedCabins & allProfile.SelectedCabins) == specific.SelectedCabins);
        }

        /*
            Creates a snapshot dictionary of the current monitored flights collection,
            mapping each unique flight profile key to its current availability and detailed status.
        */
        private System.Collections.Generic.Dictionary<string, string> GetMonitoredFlightsSnapshot()
        {
            var snapshot = new System.Collections.Generic.Dictionary<string, string>();
            lock (MonitoredFlights)
            {
                foreach (var profile in MonitoredFlights)
                {
                    string key = GetProfileKey(profile);
                    if (!snapshot.ContainsKey(key))
                    {
                        snapshot[key] = $"{profile.AvailabilityStatus}|{profile.DetailedStatus}";
                    }
                }
            }
            return snapshot;
        }

        /*
            Compares the post-run state of monitored flights against the pre-run snapshot,
            identifies any changes (new flights, status changes, wildcard resolutions),
            and sends a consolidated email notification summary asynchronously if changes are found.
        */
        private async System.Threading.Tasks.Task CompareAndSendNotificationAsync(System.Collections.Generic.Dictionary<string, string> snapshot)
        {
            var changes = new System.Collections.Generic.List<string>();
            var currentKeys = new System.Collections.Generic.HashSet<string>();

            System.Collections.Generic.List<FlightProfile> currentProfiles;
            lock (MonitoredFlights)
            {
                currentProfiles = MonitoredFlights.ToList();
            }

            foreach (var profile in currentProfiles)
            {
                string key = GetProfileKey(profile);
                currentKeys.Add(key);

                string routeInfo = $"{profile.DepartureAirport} -> {profile.ArrivalAirport} on {profile.TravelDate:yyyy-MM-dd} (Flight: {profile.FlightNumber}, Cabin: {profile.CabinClass})";

                if (snapshot.TryGetValue(key, out string? oldStatus))
                {
                    string newStatus = $"{profile.AvailabilityStatus}|{profile.DetailedStatus}";
                    if (oldStatus != newStatus)
                    {
                        string[] oldParts = oldStatus.Split('|');
                        string oldAvail = oldParts[0];
                        string oldDetail = oldParts.Length > 1 ? oldParts[1] : "TBD";

                        changes.Add($"- [STATUS CHANGED] {routeInfo}: {oldAvail} ({oldDetail}) -> {profile.AvailabilityStatus} ({profile.DetailedStatus})");
                    }
                }
                else
                {
                    changes.Add($"- [NEW FLIGHT FOUND] {routeInfo}: {profile.AvailabilityStatus} ({profile.DetailedStatus})");
                }
            }

            foreach (var kvp in snapshot)
            {
                if (!currentKeys.Contains(kvp.Key))
                {
                    string[] parts = kvp.Key.Split('-');
                    if (parts.Length >= 6)
                    {
                        string dept = parts[0];
                        string dest = parts[1];
                        string dateStr = parts[2];
                        string flight = parts[3];
                        string cabin = parts[5];

                        if (dest.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                        {
                            changes.Add($"- [WILDCARD RESOLVED] {dept} -> {dest} on {dateStr} (Flight: {flight}, Cabin: {cabin}) was checked and replaced with specific flight results.");
                        }
                        else
                        {
                            changes.Add($"- [REMOVED] {dept} -> {dest} on {dateStr} (Flight: {flight}, Cabin: {cabin})");
                        }
                    }
                }
            }

            if (changes.Count > 0)
            {
                string changeSummary = string.Join("\n", changes);
                await FlyNotify.Services.EmailService.SendStatusAlertAsync(changeSummary);
            }
        }
    }
}