using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using System;

using FlyNotify.Models;

namespace FlyNotify.Views
{
    public partial class AddFlightDialog : Window
    {
        /*
            Public property exposed to the parent window. 
            This must be declared directly under the class definition, never inside a method.
        */
        public Models.FlightProfile TargetProfile { get; private set; }

        public AddFlightDialog()
        {
            InitializeComponent();
            TargetProfile = null;
        }

        /*
            Form Submission and Input Validation Pipeline
        */
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtDeparture.Text) || TxtDeparture.Text.Length != 3)
            {
                MessageBox.Show("Please enter a valid 3-letter IATA departure airport code.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtArrival.Text))
            {
                MessageBox.Show("Please specify a valid arrival airport or region code.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!DpTravelDate.SelectedDate.HasValue)
            {
                MessageBox.Show("A valid travel tracking target date selection is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string cabinType = ((ComboBoxItem)CbCabinClass.SelectedItem).Content.ToString();
            int passengerCount = int.Parse(((ComboBoxItem)CbPassengers.SelectedItem).Content.ToString());

            TargetProfile = new Models.FlightProfile
            {
                Departure = TxtDeparture.Text.Trim().ToUpper(),
                Arrival = TxtArrival.Text.Trim().ToUpper(),
                TravelDate = DpTravelDate.SelectedDate.Value,
                FlightNumber = string.IsNullOrWhiteSpace(TxtFlightNumber.Text) ? "QF000" : TxtFlightNumber.Text.Trim().ToUpper(),
                DepartureTime = string.IsNullOrWhiteSpace(TxtDepartureTime.Text) ? "--:--" : TxtDepartureTime.Text.Trim(),
                ArrivalTime = string.IsNullOrWhiteSpace(TxtArrivalTime.Text) ? "--:--" : TxtArrivalTime.Text.Trim(),
                DurationString = "--h --m",
                CabinClass = cabinType,
                PassengerCount = passengerCount,
                AvailabilityStatus = "Pending Scrape",
                LastCheckedString = "Never"
            };

            this.DialogResult = true;
            this.Close();
        }

        /*
            Form Discard Action Handling
        */
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}