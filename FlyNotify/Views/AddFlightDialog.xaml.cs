using System;
using System.Collections.Generic;
using System.Windows;
using FlyNotify.Models;

namespace FlyNotify.Views
{
    public partial class AddFlightDialog : Window
    {
        public FlightProfile TargetProfile { get; private set; }

        public AddFlightDialog()
        {
            InitializeComponent();
            TargetProfile = null;
        }

        /*
            Form Submission and Entry Validation Pipeline Logic
        */
        /*
              Form Submission, Input Evaluation, and Data Packaging Pipeline
          */
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate that the Departure text entry contains a strict 3-letter IATA identifier
            if (string.IsNullOrWhiteSpace(TxtDeparture.Text) || TxtDeparture.Text.Trim().Length != 3)
            {
                MessageBox.Show(
                    "Please enter a valid 3-letter IATA departure airport code.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            // Ensure the Destination text input has been filled out
            if (string.IsNullOrWhiteSpace(TxtArrival.Text))
            {
                MessageBox.Show(
                    "Please specify a valid arrival airport or regional tracking code.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            // Ensure a physical chronological calendar node has been committed
            if (!DpTravelDate.SelectedDate.HasValue)
            {
                MessageBox.Show(
                    "A valid travel tracking target date selection is required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            // Compile all active Cabin Class CheckBox selections into a normalized comma-delimited string array
            var selectedCabins = new System.Collections.Generic.List<string>();

            if (ChkEconomy.IsChecked == true)
            {
                selectedCabins.Add("Economy");
            }
            if (ChkPremium.IsChecked == true)
            {
                selectedCabins.Add("Premium Economy");
            }
            if (ChkBusiness.IsChecked == true)
            {
                selectedCabins.Add("Business");
            }
            if (ChkFirst.IsChecked == true)
            {
                selectedCabins.Add("First");
            }

            // Fallback validation if the user cleared out all checkbox items
            if (selectedCabins.Count == 0)
            {
                MessageBox.Show(
                    "Please select at least one tracking cabin class category.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            string combinedCabinString = string.Join(", ", selectedCabins);

            // Determine the active mutually-exclusive Passenger count selection from the RadioButton group
            int computedPassengerCount = 1;

            if (RbPax2.IsChecked == true)
            {
                computedPassengerCount = 2;
            }
            else if (RbPax3.IsChecked == true)
            {
                computedPassengerCount = 3;
            }
            else if (RbPax4.IsChecked == true)
            {
                computedPassengerCount = 4;
            }

            // Bind the validated structural entries to your unmodified FlightProfile target model instance
            TargetProfile = new FlyNotify.Models.FlightProfile
            {
                Departure = TxtDeparture.Text.Trim().ToUpper(),
                Arrival = TxtArrival.Text.Trim().ToUpper(),
                TravelDate = DpTravelDate.SelectedDate.Value,
                CabinClass = combinedCabinString,
                PassengerCount = computedPassengerCount,

                // Standard model properties excluded from this dialog are set safely to tracking defaults
                FlightNumber = "QF000",
                DepartureTime = "--:--",
                ArrivalTime = "--:--",
                DurationString = "--h --m",
                AvailabilityStatus = "Pending Scrape",
                LastCheckedString = "Never"
            };

            // Set the dialog operational code frame results and clear visibility state
            this.DialogResult = true;
            this.Close();
        }

        /*
            Form Discard Action Control Handling
        */
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}