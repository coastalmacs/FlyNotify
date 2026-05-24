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

namespace FlyNotify
{
    /// <summary>
    /// Interaction logic for AddFlightDialog.xaml
    /// </summary>
    public partial class AddFlightDialog : Window
    {
        public AddFlightDialog()
        {
            InitializeComponent();
            TargetProfile = null;
        }


        /*
            Form Submission and Entry Validation Pipeline Logic
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

            // Capture raw structural inputs from form fields
            string cabinType = ((ComboBoxItem)CbCabinClass.SelectedItem).Content.ToString();
            int passengerCount = int.Parse(((ComboBoxItem)CbPassengers.SelectedItem).Content.ToString());

            // Build out result configuration transport payload entity
            TargetProfile = new FlightProfileResult
            {
                Departure = TxtDeparture.Text.Trim().ToUpper(),
                Arrival = TxtArrival.Text.Trim().ToUpper(),
                TravelDate = DpTravelDate.SelectedDate.Value,
                FlightNumber = string.IsNullOrWhiteSpace(TxtFlightNumber.Text) ? "QF000" : TxtFlightNumber.Text.Trim().ToUpper(),
                DepartureTime = string.IsNullOrWhiteSpace(TxtDepartureTime.Text) ? "--:--" : TxtDepartureTime.Text.Trim(),
                ArrivalTime = string.IsNullOrWhiteSpace(TxtArrivalTime.Text) ? "--:--" : TxtArrivalTime.Text.Trim(),
                CabinClass = cabinType,
                PassengerCount = passengerCount
            };

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

    /*
        Internal Structural Transport Contract representing the completed dialog selection variables
    */
    public class FlightProfileResult
    {
        public string Departure { get; set; }
        public string Arrival { get; set; }
        public DateTime TravelDate { get; set; }
        public string FlightNumber { get; set; }
        public string DepartureTime { get; set; }
        public string AlignmentTime { get; set; }
        public string ArrivalTime { get; set; }
        public string CabinClass { get; set; }
        public int PassengerCount { get; set; }

        public string TravelDateString
        {
            get
            {
                return TravelDate.ToString("yyyy-MM-dd");
            }
        }
    }
}

