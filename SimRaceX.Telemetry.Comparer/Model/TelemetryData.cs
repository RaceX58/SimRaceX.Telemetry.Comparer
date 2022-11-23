using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimRaceX.Telemetry.Comparer.Model
{
    public class TelemetryData : BaseModel
    {
        #region Fields
        private double _Throttle;
        private double _Brake;
        private double _Clutch;
        private double _LapDistance;
        private double _Speed;
        private string _Gear;
        private double? _SteeringAngle;
        #endregion

        #region Properties
        public double Throttle
        {
            get { return _Throttle; }
            set { _Throttle = value; OnPropertyChanged(nameof(Throttle)); }
        }
        public double Brake
        {
            get { return _Brake; }
            set { _Brake = value; OnPropertyChanged(nameof(Brake)); }
        }
        public double Clutch
        {
            get { return _Clutch; }
            set { _Clutch = value; OnPropertyChanged(nameof(Clutch)); }
        }
        public double LapDistance
        {
            get { return _LapDistance; }
            set { _LapDistance = value; OnPropertyChanged(nameof(LapDistance)); }
        }
        public double Speed
        {
            get { return _Speed; }
            set { _Speed = value; OnPropertyChanged(nameof(Speed)); }
        }
        public string Gear
        {
            get { return _Gear; }
            set { _Gear = value; OnPropertyChanged(nameof(Gear)); }
        }
        public double? SteeringAngle
        {
            get { return _SteeringAngle; }
            set { _SteeringAngle = value; OnPropertyChanged(nameof(SteeringAngle)); }
        }
        #endregion
    }
}
