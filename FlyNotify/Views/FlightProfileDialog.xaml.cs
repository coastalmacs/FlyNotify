using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using FlyNotify.Models;
using MessageBox = System.Windows.MessageBox;

namespace FlyNotify.Views
{
    public partial class FlightProfileDialog : Window
    {
        private readonly FlightProfile? _originalProfile;

        /*
            Delayed initialization property assigned cleanly once form parameters
            comply with strict routing validation checkpoints.
        */
        public FlightProfile? TargetProfile { get; private set; }

        public FlightProfileDialog(FlightProfile? profileToEdit = null)
        {
            InitializeComponent();
            TargetProfile = null;
            _originalProfile = profileToEdit;

            if (_originalProfile != null)
            {
                // Pre-populate fields with existing profile values
                TxtDeparture.Text = _originalProfile.DepartureAirport;
                TxtArrival.Text = _originalProfile.ArrivalAirport;
                DpTravelStartDate.SelectedDate = _originalProfile.TravelDate;
                DpTravelEndDate.SelectedDate = _originalProfile.TravelEndDate;

                // Set Cabin CheckBoxes
                ChkEconomy.IsChecked = _originalProfile.SelectedCabins.HasFlag(CabinClasses.Economy);
                ChkPremium.IsChecked = _originalProfile.SelectedCabins.HasFlag(CabinClasses.PremiumEconomy);
                ChkBusiness.IsChecked = _originalProfile.SelectedCabins.HasFlag(CabinClasses.Business);
                ChkFirst.IsChecked = _originalProfile.SelectedCabins.HasFlag(CabinClasses.First);

                // Set Passenger RadioButtons
                RbPax1.IsChecked = _originalProfile.PassengerCount == 1;
                RbPax2.IsChecked = _originalProfile.PassengerCount == 2;
                RbPax3.IsChecked = _originalProfile.PassengerCount == 3;
                RbPax4.IsChecked = _originalProfile.PassengerCount == 4;
            }
            else
            {
                // Seed default layout values to improve input scheduling speed for new profiles
                DpTravelStartDate.SelectedDate = DateTime.Today;
                DpTravelEndDate.SelectedDate = DateTime.Today;
            }
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

            // 5. Ensure valid travel tracking target date selection range is configured
            if (!DpTravelStartDate.SelectedDate.HasValue || !DpTravelEndDate.SelectedDate.HasValue)
            {
                MessageBox.Show(
                    "Both travel tracking start and end date selections are required.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            if (DpTravelEndDate.SelectedDate.Value < DpTravelStartDate.SelectedDate.Value)
            {
                MessageBox.Show(
                    "The travel end date cannot be earlier than the travel start date. Please specify a valid date range.",
                    "Date Range Configuration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            if ((DpTravelEndDate.SelectedDate.Value - DpTravelStartDate.SelectedDate.Value).Days > 6)
            {
                MessageBox.Show(
                    "The travel date range cannot exceed 7 days. Please specify a valid date range.",
                    "Date Range Configuration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            // 6. Compile active CheckBox selections into our type-safe bitwise flags container
            CabinClasses chosenCabins = CabinClasses.None;

            if (ChkEconomy.IsChecked is true)
            {
                chosenCabins |= CabinClasses.Economy;
            }
            if (ChkPremium.IsChecked is true)
            {
                chosenCabins |= CabinClasses.PremiumEconomy;
            }
            if (ChkBusiness.IsChecked is true)
            {
                chosenCabins |= CabinClasses.Business;
            }
            if (ChkFirst.IsChecked is true)
            {
                chosenCabins |= CabinClasses.First;
            }

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

            if (RbPax2.IsChecked is true)
            {
                computedPassengerCount = 2;
            }
            else if (RbPax3.IsChecked is true)
            {
                computedPassengerCount = 3;
            }
            else if (RbPax4.IsChecked is true)
            {
                computedPassengerCount = 4;
            }

            // Check if key tracking settings were modified compared to original
            bool isKeyUnchanged = _originalProfile != null &&
                                  _originalProfile.DepartureAirport == sanitizedDeparture &&
                                  _originalProfile.ArrivalAirport == sanitizedArrival &&
                                  _originalProfile.TravelDate == DpTravelStartDate.SelectedDate.Value &&
                                  _originalProfile.TravelEndDate == DpTravelEndDate.SelectedDate.Value &&
                                  _originalProfile.SelectedCabins == chosenCabins &&
                                  _originalProfile.PassengerCount == computedPassengerCount;

            // 8. Bind the validated configurations directly to your updated multi-cabin profile instance contract
            TargetProfile = new FlightProfile
            {
                DepartureAirport = sanitizedDeparture,
                ArrivalAirport = sanitizedArrival,
                TravelDate = DpTravelStartDate.SelectedDate.Value,
                TravelEndDate = DpTravelEndDate.SelectedDate.Value,
                SelectedCabins = chosenCabins,
                PassengerCount = computedPassengerCount,

                // If editing and key fields didn't change, retain scraped values. Otherwise, reset to TBD.
                FlightNumber = isKeyUnchanged ? _originalProfile!.FlightNumber : "TBD",
                DepartureTime = isKeyUnchanged ? _originalProfile!.DepartureTime : "TBD",
                ArrivalTime = isKeyUnchanged ? _originalProfile!.ArrivalTime : "TBD",
                Duration = isKeyUnchanged ? _originalProfile!.Duration : "TBD",
                TargetCabin = isKeyUnchanged ? _originalProfile!.TargetCabin : "TBD",
                AvailabilityStatus = isKeyUnchanged ? _originalProfile!.AvailabilityStatus : "TBD",
                DetailedStatus = isKeyUnchanged ? _originalProfile!.DetailedStatus : "TBD",
                LastChecked = isKeyUnchanged ? _originalProfile!.LastChecked : DateTime.MinValue
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

        /*
            Synchronizes the travel start and end dates when one of them is updated 
            to prevent invalid date ranges.
        */
        private void DpTravelStartDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DpTravelStartDate.SelectedDate.HasValue && DpTravelEndDate.SelectedDate.HasValue)
            {
                if (DpTravelStartDate.SelectedDate.Value > DpTravelEndDate.SelectedDate.Value)
                {
                    DpTravelEndDate.SelectedDate = DpTravelStartDate.SelectedDate;
                }
                else if ((DpTravelEndDate.SelectedDate.Value - DpTravelStartDate.SelectedDate.Value).Days > 6)
                {
                    DpTravelEndDate.SelectedDate = DpTravelStartDate.SelectedDate.Value.AddDays(6);
                }
            }
        }

        private void DpTravelEndDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DpTravelStartDate.SelectedDate.HasValue && DpTravelEndDate.SelectedDate.HasValue)
            {
                if (DpTravelEndDate.SelectedDate.Value < DpTravelStartDate.SelectedDate.Value)
                {
                    DpTravelStartDate.SelectedDate = DpTravelEndDate.SelectedDate;
                }
                else if ((DpTravelEndDate.SelectedDate.Value - DpTravelStartDate.SelectedDate.Value).Days > 6)
                {
                    DpTravelStartDate.SelectedDate = DpTravelEndDate.SelectedDate.Value.AddDays(-6);
                }
            }
        }
    }
}
