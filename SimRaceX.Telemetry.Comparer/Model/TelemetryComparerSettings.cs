using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Permissions;
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
        private bool _ShowGauges;
        private bool _ShowSteeringAngle;
        private Dictionary<int, string> _ComparisonModes = new Dictionary<int, string>()
        {
            {0, "Personal best" },
            {1, "Session best" },
            {2, "Best of friend" }
        };
        private KeyValuePair<int,string> _SelectedComparisonMode;
        private bool _PersonalBestDiscardInvalidLap;
        private bool _SessionBestDiscardInvalidLap;
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
        public bool ShowSteeringAngle
        {
            get { return _ShowSteeringAngle; }
            set { _ShowSteeringAngle = value; OnPropertyChanged(nameof(ShowSteeringAngle)); }
        }
        public Dictionary<int, string> ComparisonModes
        {
            get { return _ComparisonModes; }
        }       
        public KeyValuePair<int, string> SelectedComparisonMode
        {
            get { return _SelectedComparisonMode; }
            set 
            { 
                _SelectedComparisonMode = value;
                SelectedComparisonModeChanged?.Invoke(this, null);
                OnPropertyChanged(nameof(SelectedComparisonMode)); 
            }
        }
        public bool PersonalBestDiscardInvalidLap
        {
            get { return _PersonalBestDiscardInvalidLap; }
            set { _PersonalBestDiscardInvalidLap = value; OnPropertyChanged(nameof(PersonalBestDiscardInvalidLap)); }
        }
        public bool SessionBestDiscardInvalidLap
        {
            get { return _SessionBestDiscardInvalidLap; }
            set { _SessionBestDiscardInvalidLap = value; OnPropertyChanged(nameof(SessionBestDiscardInvalidLap)); }
        }
        #endregion

        #region Events
        public event EventHandler SelectedComparisonModeChanged;
        #endregion
    }
}
