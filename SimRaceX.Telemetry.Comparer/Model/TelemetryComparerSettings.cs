using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimRaceX.Telemetry.Comparer.Model
{
    public class TelemetryComparerSettings : BaseModel
    {
        #region Fields
        private ObservableCollection<CarTrackTelemetry> _CarTrackTelemetries = new ObservableCollection<CarTrackTelemetry>();
        private bool _ShowBrakeTrace;
        private bool _ShowThrottleTrace;
        private bool _ShowSpeedTrace;
        private bool _ShowGear;
        private bool _ShowGauges;
        #endregion

        #region Properties
        public ObservableCollection<CarTrackTelemetry> CarTrackTelemetries
        {
            get { return _CarTrackTelemetries; }
            set { _CarTrackTelemetries = value; OnPropertyChanged(nameof(CarTrackTelemetries)); }
        }
        public bool ShowBrakeTrace
        {
            get { return _ShowBrakeTrace; }
            set { _ShowBrakeTrace = value; OnPropertyChanged(nameof(ShowBrakeTrace)); }
        }
        public bool ShowThrottleTrace
        {
            get { return _ShowThrottleTrace; }
            set { _ShowThrottleTrace = value; OnPropertyChanged(nameof(ShowThrottleTrace)); }
        }
        public bool ShowSpeedTrace
        {
            get { return _ShowSpeedTrace; }
            set { _ShowSpeedTrace = value; OnPropertyChanged(nameof(ShowSpeedTrace)); }
        }
        public bool ShowGauges
        {
            get { return _ShowGauges; }
            set { _ShowGauges = value; OnPropertyChanged(nameof(ShowGauges)); }
        }
        #endregion
    }
}
