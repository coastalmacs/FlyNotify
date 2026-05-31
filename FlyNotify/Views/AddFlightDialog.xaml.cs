using FlyNotify.Models;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace FlyNotify.Views
{
    public partial class AddFlightDialog : Window
    {
        public FlightProfile TargetProfile { get; private set; }

        public AddFlightDialog()
        {
            InitializeComponent();
            TargetProfile = null;

            DpTravelDate.SelectedDate = DateTime.Today;

        }


        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Clean and normalize the user input text blocks to avoid trailing space mismatch bugs
            string sanitizedDeparture = TxtDeparture.Text.Trim().ToUpper();
            string sanitizedArrival = TxtArrival.Text.Trim().ToUpper();

            // 2. Core Operational Validation check ensuring origin and destination routes are not identical
            if (sanitizedDeparture == sanitizedArrival && !string.IsNullOrEmpty(sanitizedDeparture))
            {
                MessageBox.Show(
                    "The departure airport and arrival airport codes cannot be identical. Please specify a valid route sequence.",
                    "Route Configuration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                // Highlight the error location by focusing the arrival input field back to the user
                TxtArrival.Focus();
                TxtArrival.SelectAll();

                return; // Aborts form saving and breaks execution sequence safely
            }

            // 3. (Optional Baseline Checks) Keep your standard empty boundary tracking logic below
            if (string.IsNullOrEmpty(sanitizedDeparture) || string.IsNullOrEmpty(sanitizedArrival))
            {
                MessageBox.Show(
                    "Please fill out both departure and arrival fields before saving.",
                    "Missing Information",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            if (DpTravelDate.SelectedDate == null)
            {
                MessageBox.Show(
                    "Please select a valid travel date before saving.",
                    "Missing Information",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            // --- Compilation Engine Integration ---
            // If all validation rules clear successfully, construct the domain instance contract
            CabinClasses chosenCabins = CabinClasses.None;
            if (ChkEconomy.IsChecked == true) { chosenCabins |= CabinClasses.Economy; }
            if (ChkPremium.IsChecked == true) { chosenCabins |= CabinClasses.PremiumEconomy; }
            if (ChkBusiness.IsChecked == true) { chosenCabins |= CabinClasses.Business; }
            if (ChkFirst.IsChecked == true) { chosenCabins |= CabinClasses.First; }

            if (chosenCabins == CabinClasses.None)
            {
                MessageBox.Show(
                    "Please select at least one cabin class tier to monitor.",
                    "Missing Information",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            int passengers = 1;
            int.TryParse(TxtPassengers.Text, out passengers);

            TargetProfile = new FlightProfile
            {
                DepartureAirport = sanitizedDeparture,
                ArrivalAirport = sanitizedArrival,
                TravelDate = DpTravelDate.SelectedDate.Value,
                SelectedCabins = chosenCabins,
                PassengerCount = passengers,
                AvailabilityStatus = "TBD"
            };

            // Set dialog completion output flags and close out active modal view channel
            DialogResult = true;
            Close();
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