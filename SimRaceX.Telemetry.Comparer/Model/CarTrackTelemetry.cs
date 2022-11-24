using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimRaceX.Telemetry.Comparer.Model
{
    public class CarTrackTelemetry : BaseModel
    {
        #region Fields
        private string _GameName;
        private string _CarName;
        private string _TrackName;
        private List<TelemetryData> _TelemetryDatas;
        private TimeSpan _LapTime;
        private string _PlayerName;
        private string _TrackCode;
        //private bool _UseAsReferenceLap;
        #endregion

        #region Properties
        public string GameName
        {
            get { return _GameName; }
            set { _GameName = value; OnPropertyChanged(nameof(GameName)); }
        }
        public string CarName
        {
            get { return _CarName; }
            set { _CarName = value; OnPropertyChanged(nameof(CarName)); }
        }
        public string TrackName
        {
            get { return _TrackName; }
            set { _TrackName = value; OnPropertyChanged(nameof(TrackName)); }
        }
        public List<TelemetryData> TelemetryDatas
        {
            get { return _TelemetryDatas; }
            set { _TelemetryDatas = value; OnPropertyChanged(nameof(TelemetryDatas)); }
        }
        public TimeSpan LapTime
        {
            get { return _LapTime; }
            set { _LapTime = value; OnPropertyChanged(nameof(LapTime)); }
        }
        public string PlayerName
        {
            get { return _PlayerName; }
            set { _PlayerName = value; OnPropertyChanged(nameof(PlayerName)); }
        }
        public string TrackCode
        {
            get { return _TrackCode; }
            set { _TrackCode = value; OnPropertyChanged(nameof(TrackCode)); }
        }
        //public bool UseAsReferenceLap
        //{
        //    get { return _UseAsReferenceLap; }
        //    set { _UseAsReferenceLap = value; OnPropertyChanged(nameof(UseAsReferenceLap));}
        //}
        public string FormattedPlayerNameLapTime
        {
            get { return $"{PlayerName} - {LapTime.ToString(@"mm\:ss\.fff")}"; }
        }
        #endregion

        #region Cosntructor
        public CarTrackTelemetry()
        {
            _TelemetryDatas = new List<TelemetryData>();
        }
        #endregion
    }
}
