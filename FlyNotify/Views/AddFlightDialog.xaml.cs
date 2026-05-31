using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using FlyNotify.Models;

namespace FlyNotify.Views
{
    public partial class AddFlightDialog : Window
    {
        /*
            Delayed initialization property assigned cleanly once form parameters
            comply with strict routing validation checkpoints.
        */
        public FlightProfile? TargetProfile { get; private set; }

        public AddFlightDialog()
        {
            InitializeComponent();
            TargetProfile = null;

            // Seed default layout value to improve input scheduling speed
            DpTravelDate.SelectedDate = DateTime.Today;
        }

        /*
            Form Submission, Input Evaluation, and Data Packaging Pipeline
        */
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Clean and normalize raw user entry inputs to avoid trailing space text mismatch issues
            string sanitizedDeparture = TxtDeparture.Text.Trim().ToUpper();
            string sanitizedArrival = TxtArrival.Text.Trim().ToUpper();

            // 2. Validate that the Departure text entry contains a strict 3-character IATA identifier
            if (string.IsNullOrWhiteSpace(sanitizedDeparture) || sanitizedDeparture.Length != 3)
            {
                MessageBox.Show(
                    "Please enter a valid 3-letter IATA departure airport code.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            // 3. Ensure the Destination text input has been completely filled out
            if (string.IsNullOrWhiteSpace(sanitizedArrival))
            {
                MessageBox.Show(
                    "Please specify a valid arrival airport or regional tracking code.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            // 4. Core Operational Validation check ensuring origin and destination routes are not identical
            if (sanitizedDeparture == sanitizedArrival)
            {
                MessageBox.Show(
                    "The departure airport and arrival airport codes cannot be identical. Please specify a valid route sequence.",
                    "Route Configuration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                // Focus and select the problematic text element to simplify error correction
                TxtArrival.Focus();
                TxtArrival.SelectAll();
                return;
            }

            // 5. Ensure a physical chronological calendar node has been selected
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

            // 6. Compile active CheckBox selections into our type-safe bitwise flags container
            CabinClasses chosenCabins = CabinClasses.None;

            if (ChkEconomy.IsChecked == true) { chosenCabins |= CabinClasses.Economy; }
            if (ChkPremium.IsChecked == true) { chosenCabins |= CabinClasses.PremiumEconomy; }
            if (ChkBusiness.IsChecked == true) { chosenCabins |= CabinClasses.Business; }
            if (ChkFirst.IsChecked == true) { chosenCabins |= CabinClasses.First; }

            // Fallback interception if the user cleared out all checkbox criteria options
            if (chosenCabins == CabinClasses.None)
            {
                MessageBox.Show(
                    "Please select at least one tracking cabin class category.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            // 7. Determine the active mutually-exclusive Passenger count selection from the RadioButton group
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

            // 8. Bind the validated configurations directly to your updated multi-cabin profile instance contract
            TargetProfile = new FlightProfile
            {
                DepartureAirport = sanitizedDeparture,
                ArrivalAirport = sanitizedArrival,
                TravelDate = DpTravelDate.SelectedDate.Value,
                SelectedCabins = chosenCabins, // Correct: Pipes the enum mask into bitwise flags storage
                PassengerCount = computedPassengerCount,

                // System properties excluded from this form scope use standardized baseline markers
                FlightNumber = "TBD",
                DepartureTime = "TBD",
                ArrivalTime = "TBD",
                Duration = "TBD",
                AvailabilityStatus = "TBD",
                LastChecked = DateTime.MinValue
            };

            // Set the modal outcome to true and close the dialog channel frame safely
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