﻿using GameReaderCommon;
using iRacingSDK;
using OxyPlot;
using OxyPlot.Series;
using SimHub.Plugins;
using SimRaceX.Telemetry.Comparer.Commands;
using SimRaceX.Telemetry.Comparer.Helpers;
using SimRaceX.Telemetry.Comparer.Model;
using SimRaceX.Telemetry.Comparer.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using static SimHub.Plugins.UI.SupportedGamePicker;

using System.IO;
using System.Diagnostics;
using Newtonsoft.Json;
using FMOD;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using SimHub.Plugins.DataPlugins.PersistantTracker;
using static System.Net.Mime.MediaTypeNames;
using System.Windows.Shapes;
using WoteverCommon;
using System.IO.Ports;
using OxyPlot.Axes;
using System.Windows.Markup;

namespace SimRaceX.Telemetry.Comparer.ViewModel
{
    [PluginDescription("Compare your lap using telemetry")]
    [PluginAuthor("RaceX")]
    [PluginName("SimRaceX Telemetry Comparer")]
    public class TelemetryComparerPlugin : ObservableObject, IPlugin, IDataPlugin, IWPFSettings
    {
        #region Fields
        int tickCount;
        private TelemetryComparerView _View;
        private TelemetryComparerSettings _Settings;
        private static object _syncLock = new object();
        private List<TelemetryData> _CurrentLapTelemetry;
        private CarTrackTelemetry _SelectedCarTrackTelemetry;
        private CarTrackTelemetry _CurrentSessionBestTelemetry;
        private CarTrackTelemetry _PersonalBestTelemetry;
        private CarTrackTelemetry _SelectedBestOfFriendTelemetry;
        private PlotModel _TelemetryPlotModel;
        private ObservableCollection<DataPoint> _ThrottleLineSeries;
        private ObservableCollection<DataPoint> _BrakeLineSeries;
        private Guid _CurrentSessionId;
        private double? _SteeringAngle;
        private int _IncidentCount;
        private CarTrackTelemetry _SelectedViewTelemetry;
        private bool _CurrentLapHasIncidents;
        private bool _FilterTelemetries;
        private int _IsInpit = 1;
        #endregion

        #region Properties
        public PluginManager PluginManager { get; set; }
        public ImageSource PictureIcon => this.ToIcon(Properties.Resources.telemetry_comparer_icon);
        public string LeftMenuTitle => "Telemetry Comparer";
        public TelemetryComparerSettings Settings
        {
            get { return _Settings; }
            set { _Settings = value; OnPropertyChanged(nameof(Settings)); }
        }
        public CarTrackTelemetry SelectedCarTrackTelemetry
        {
            get { return _SelectedCarTrackTelemetry; }
            set 
            { 
                _SelectedCarTrackTelemetry = value;
                SelectedCarTrackTelemetryChanged?.Invoke(this, null);
                OnPropertyChanged(nameof(SelectedCarTrackTelemetry)); 
            }
        }
        public CarTrackTelemetry SelectedViewTelemetry
        {
            get { return _SelectedViewTelemetry; }
            set
            {
                _SelectedViewTelemetry = value;
                if (_SelectedCarTrackTelemetry != null)
                {
                    GetSelectedViewTelemetryDatas(true);
                }
                OnPropertyChanged(nameof(SelectedViewTelemetry));
            }
        }

        public CarTrackTelemetry CurrentSessionBestTelemetry
        {
            get { return _CurrentSessionBestTelemetry; }
            set
            {
                _CurrentSessionBestTelemetry = value;             
                OnPropertyChanged(nameof(CurrentSessionBestTelemetry));
            }
        }
        public CarTrackTelemetry PersonalBestTelemetry
        {
            get { return _PersonalBestTelemetry; }
            set
            {
                _PersonalBestTelemetry = value;
                OnPropertyChanged(nameof(PersonalBestTelemetry));
            }
        }

        public PlotModel TelemetryPlotModel
        {
            get { return _TelemetryPlotModel; }
            set { _TelemetryPlotModel = value; OnPropertyChanged(nameof(TelemetryPlotModel)); }
        }
        public ObservableCollection<DataPoint> ThrottleLineSeries
        {
            get { return _ThrottleLineSeries; }
            set { _ThrottleLineSeries = value; OnPropertyChanged(nameof(ThrottleLineSeries)); }
        }
        public ObservableCollection<DataPoint> BrakeLineSeries
        {
            get { return _BrakeLineSeries; }
            set { _BrakeLineSeries = value; OnPropertyChanged(nameof(BrakeLineSeries)); }
        }
        public Guid CurrentSessionId
        {
            get { return _CurrentSessionId; }
            set 
            {
                _CurrentSessionId = value;
                CurrentSessionChanged?.Invoke(this, null);
                OnPropertyChanged(nameof(CurrentSessionId));
            }
        }
        public double? SteeringAngle
        {
            get { return _SteeringAngle; }
            set
            {
                _SteeringAngle = value;              
                OnPropertyChanged(nameof(SteeringAngle));
            }
        }
        public int IncidentCount
        {
            get { return _IncidentCount; }
            set
            {
                _IncidentCount = value;                
                OnPropertyChanged(nameof(IncidentCount));
            }
        }
        public bool IsPersonalBestMode
        {
            get
            {
                if (Settings.SelectedComparisonMode.Equals(default))
                    return false;
                return Settings.SelectedComparisonMode.Key == 0;
            }
        }
        public bool IsSessionBestMode
        {
            get
            {
                if (Settings.SelectedComparisonMode.Equals(default))
                    return false;
                return Settings.SelectedComparisonMode.Key == 1;
            }
        }
        public bool IsBestOfFriendMode
        {
            get
            {
                if (Settings.SelectedComparisonMode.Equals(default))
                    return false;
                return Settings.SelectedComparisonMode.Key == 2;
            }
        }
       
        public List<CarTrackTelemetry> AvailableBestOfFriendTelemetries
        {
            get
            {
                if (PluginManager.LastData is null)
                    return null;
                if (PluginManager.LastData.NewData is null)
                    return null;
                string gameName = PluginManager.LastData.GameName;
                string carModel = PluginManager.LastData.NewData.CarModel;
                string trackCode = PluginManager.LastData.NewData.TrackCode;
                string playerName = PluginManager.LastData.NewData.PlayerName;

                return _Settings.CarTrackTelemetries.Where(x =>
                                        x.GameName == gameName
                                        && x.TrackCode == trackCode
                                        && x.CarName == carModel
                                        && x.PlayerName != playerName
                                        ).ToList();
            }
        }
        public CarTrackTelemetry SelectedBestOfFriendTelemetry
        {
            get
            {
                if (_SelectedCarTrackTelemetry is null)
                    _SelectedCarTrackTelemetry = GetPersonalBestTelemetry();
                return _SelectedBestOfFriendTelemetry; 
            }
            set
            {
                _SelectedBestOfFriendTelemetry = value;
                SetReferenceLap();
                OnPropertyChanged(nameof(SelectedBestOfFriendTelemetry));
            }
        }
        public bool CurrentLapHasIncidents
        {
            get { return _CurrentLapHasIncidents; }
            set { _CurrentLapHasIncidents = value; OnPropertyChanged(nameof(CurrentLapHasIncidents)); }
        }
        public bool IsCurrentLapValid
        {
            get
            {
                if (Settings.SelectedComparisonMode.Equals(default))
                    return false;
                if (Settings.SelectedComparisonMode.Key == 0)
                    return !Settings.PersonalBestDiscardInvalidLap || !_CurrentLapHasIncidents;
                if (Settings.SelectedComparisonMode.Key == 1)
                    return !Settings.SessionBestDiscardInvalidLap || !_CurrentLapHasIncidents;
                return true;
            }
        }
        public bool FilterTelemetries
        {
            get { return _FilterTelemetries; }
            set { _FilterTelemetries = value; OnPropertyChanged(nameof(FilterTelemetries)); OnPropertyChanged(nameof(FilteredTelemetries)); }
        }
        public List<CarTrackTelemetry> FilteredTelemetries
        {
            get
            {
                if (_FilterTelemetries && PluginManager.LastData != null && PluginManager.LastData.NewData != null)
                {

                    string gameName = PluginManager.LastData.GameName;
                    string carModel = PluginManager.LastData.NewData.CarModel;
                    string trackCode = PluginManager.LastData.NewData.TrackCode;
                    return _Settings.CarTrackTelemetries.Where(x =>
                                   x.GameName == gameName
                                   && x.TrackCode == trackCode
                                   && x.CarName == carModel
                                   ).ToList();
                }
                else
                    return _Settings.CarTrackTelemetries.ToList();
            }
        }
        public int IsInpit
        {
            get { return _IsInpit; }
            set 
            { 
                if (_IsInpit == value) return;

                _IsInpit = value;
                IsInPitChanged?.Invoke(this, null);
                OnPropertyChanged(nameof(IsInpit)); 
            }
        }
        #endregion

        #region Commands

        #region DeleteCarTrackTelemetryCommand
        private DelegateCommand<CarTrackTelemetry> _DeleteCarTrackTelemetryCommand;
        public ICommand DeleteCarTrackTelemetryCommand
        {
            get
            {
                if (_DeleteCarTrackTelemetryCommand == null)
                    _DeleteCarTrackTelemetryCommand = new DelegateCommand<CarTrackTelemetry>(new System.Action<CarTrackTelemetry>(DeleteCarTrackTelemetryExecuted), 
                        new Func<CarTrackTelemetry, bool>(DeleteCarTrackTelemetryCanExecute));
                return _DeleteCarTrackTelemetryCommand;
            }

        }
        public bool DeleteCarTrackTelemetryCanExecute(CarTrackTelemetry carTrackTelemetry)
        {
            return true;
        }
        public void DeleteCarTrackTelemetryExecuted(CarTrackTelemetry carTrackTelemetry)
        {
            if (MessageBox.Show("Do you really want to delete this entry?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                return;

            Settings.CarTrackTelemetries.Remove(carTrackTelemetry);
            this.SaveCommonSettings("GeneralSettings", Settings);

            if (PluginManager.LastData.GameRunning)
            {
                if (SelectedCarTrackTelemetry == carTrackTelemetry)
                    ResetReferenceLap();

                //SelectedCarTrackTelemetry = null;

                //if (carTrackTelemetry.PlayerName != PluginManager.LastData.NewData.PlayerName)
                //{
                //    var item = Settings.CarTrackTelemetries.FirstOrDefault(
                //    x => x.GameName == carTrackTelemetry.GameName 
                //    && x.TrackCode == carTrackTelemetry.TrackCode 
                //    && x.CarName == carTrackTelemetry.CarName
                //    && x.PlayerName == PluginManager.LastData.NewData.PlayerName
                //    );
                //    if (item != null)
                //    {
                //        SelectedCarTrackTelemetry = item;
                //    }
                //}
                //else
                //{
                    ResetReferenceLap();                  
                //}
               
            }



        }
        #endregion

        #region ExportCarTrackTelemetryCommand
        private DelegateCommand _ExportCarTrackTelemetryCommand;
        public ICommand ExportCarTrackTelemetryCommand
        {
            get
            {
                if (_ExportCarTrackTelemetryCommand == null)
                    _ExportCarTrackTelemetryCommand = new DelegateCommand(new System.Action(ExportCarTrackTelemetryExecuted),
                        new Func<bool>(ExportCarTrackTelemetryCanExecute));
                return _ExportCarTrackTelemetryCommand;
            }

        }
        public bool ExportCarTrackTelemetryCanExecute()
        {
            return _SelectedCarTrackTelemetry != null;
        }
        public void ExportCarTrackTelemetryExecuted()
        {
            string exportDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports");
            if (!Directory.Exists(exportDir))
                Directory.CreateDirectory(exportDir);
            string fileName = System.IO.Path.Combine(exportDir, $"{_SelectedCarTrackTelemetry.PlayerName}_{_SelectedCarTrackTelemetry.TrackName}_{_SelectedCarTrackTelemetry.CarName}.json");
            using (StreamWriter file = File.CreateText(fileName))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(file, _SelectedCarTrackTelemetry);
            }
            Process.Start(exportDir);

        }
        #endregion

        #region ImportCarTrackTelemetryCommand
        private DelegateCommand _ImportCarTrackTelemetryCommand;
        public ICommand ImportCarTrackTelemetryCommand
        {
            get
            {
                if (_ImportCarTrackTelemetryCommand == null)
                    _ImportCarTrackTelemetryCommand = new DelegateCommand(new System.Action(ImportCarTrackTelemetryExecuted),
                        new Func<bool>(ImportCarTrackTelemetryCanExecute));
                return _ImportCarTrackTelemetryCommand;
            }

        }
        public bool ImportCarTrackTelemetryCanExecute()
        {
            return true;
        }
        public void ImportCarTrackTelemetryExecuted()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Json files (*.json)|*.json";
            if (openFileDialog.ShowDialog() == false)
                return;

            foreach(string file in openFileDialog.FileNames)
            {
                string content = File.ReadAllText(file);
                CarTrackTelemetry carTrackTelemetry = JsonConvert.DeserializeObject<CarTrackTelemetry>(content);
                CarTrackTelemetry existingTelemetry = Settings.CarTrackTelemetries.FirstOrDefault(x =>
                x.GameName == carTrackTelemetry.GameName
                && x.TrackName == carTrackTelemetry.TrackName
                && x.CarName == carTrackTelemetry.CarName
                && x.PlayerName == carTrackTelemetry.PlayerName);

                if  (existingTelemetry != null
                    && MessageBox.Show("An entry already exist for this combo. It will overwrite the existing datas. Do you still wan't to import it?", "Entry already exist", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                    return;

                if (existingTelemetry != null)
                    Settings.CarTrackTelemetries.Remove(existingTelemetry);

                Settings.CarTrackTelemetries.Add(carTrackTelemetry);
                this.SaveCommonSettings("GeneralSettings", Settings);

            }
        }
        #endregion

        #endregion

        #region Methods
        public void Init(PluginManager pluginManager)
        {           

            PluginManager = pluginManager;
            SimHub.Logging.Current.Info("Starting plugin SimRaceX.Telemetry.Comparer");
            Settings = this.ReadCommonSettings<TelemetryComparerSettings>("GeneralSettings", () => new TelemetryComparerSettings());

            ThrottleLineSeries = new ObservableCollection<DataPoint>();
            BrakeLineSeries = new ObservableCollection<DataPoint>();

            BindingOperations.EnableCollectionSynchronization(Settings.CarTrackTelemetries, _syncLock);

            InitSimHubProperties();
            InitSimHubEvents();
            InitSimHubActions();


            if (Settings.SelectedComparisonMode.Equals(default))
                Settings.SelectedComparisonMode = Settings.ComparisonModes.First();

            Settings.PropertyChanged += Settings_PropertyChanged;
            Settings.SelectedComparisonModeChanged += Settings_SelectedComparisonModeChanged;
            Settings.CarTrackTelemetries.CollectionChanged += CarTrackTelemetries_CollectionChanged;

            CurrentSessionChanged += TelemetryComparerPlugin_CurrentSessionChanged;
            IncidentCountChanged += TelemetryComparerPlugin_IncidentCountChanged;
            SelectedCarTrackTelemetryChanged += TelemetryComparerPlugin_SelectedCarTrackTelemetryChanged;
            IsInPitChanged += TelemetryComparerPlugin_IsInPitChanged;

          
            //SetPropertyChanged();

            OnPropertyChanged(nameof(FilteredTelemetries));
        }
        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            if (!data.GameRunning)
            {
                IsInpit = 1;
                return;
            }
                


            string gameName = data.GameName;
            if (gameName == "IRacing")
            {
                SteeringAngle = Convert.ToDouble(pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Telemetry.SteeringWheelAngle")) * -58.0;
                var incidentCount = Convert.ToInt32(pluginManager.GetPropertyValue("DataCorePlugin.GameRawData.Telemetry.PlayerCarMyIncidentCount"));
                if (incidentCount > IncidentCount)
                {
                    CurrentLapHasIncidents = true;
                    IncidentCount = incidentCount;
                }
            }
            else
                SteeringAngle = null;
            UpdateSteeringAngle();

            if (!data.NewData.Spectating && data.OldData != null)
            {
                //update the current session id
                if (data.SessionId != CurrentSessionId)
                    CurrentSessionId = data.SessionId;

             
                string carModel = data.NewData.CarModel;
                string trackName = data.NewData.TrackName;
                string playerName = data.NewData.PlayerName;
                string trackCode = data.NewData.TrackCode;

                if (data.GameName != null && data.NewData.CarModel != null && data.NewData.TrackId != null)
                {
                    //If not reference lap loaded, try to load a reference lap
                    if (SelectedCarTrackTelemetry is null && IsInpit == 0)
                        SetReferenceLap();

                    //Main loop
                    if (data.OldData.CurrentLap != data.NewData.CurrentLap
                        || tickCount == (SimHub.Licensing.LicenseManager.IsValid ? 3 : 0))
                    {
                        IsInpit = data.NewData.IsInPit;

                        if (IsInpit == 0)
                        {
                            //If a property has changed
                            if (SelectedCarTrackTelemetry != null
                                &&
                                  (
                                  SelectedCarTrackTelemetry.GameName != gameName
                                  || SelectedCarTrackTelemetry.CarName != carModel
                                  || SelectedCarTrackTelemetry.TrackCode != trackCode

                                  )
                                )
                                //try to load a reference lap
                                SetReferenceLap();
                            //If current lap has changed
                            if (data.OldData.CurrentLap != data.NewData.CurrentLap)
                            {
                                //Check the current lap telemetry is valid
                                if (_CurrentLapTelemetry != null && _CurrentLapTelemetry.Count > 0 && IsCurrentLapValid)
                                {
                                    double firstDataDistance = _CurrentLapTelemetry.First().LapDistance;
                                    double lastDataDistance = _CurrentLapTelemetry.Last().LapDistance;
                                    //Try to check if a complete lap has be done
                                    if (firstDataDistance < 0.1 && lastDataDistance > 0.9 && data.OldData.IsLapValid)
                                    {
                                        //If no reference lap is set, set current lap as reference lap
                                        if (SelectedCarTrackTelemetry is null)
                                        {
                                            _SelectedCarTrackTelemetry = new CarTrackTelemetry
                                            {
                                                GameName = gameName,
                                                CarName = carModel,
                                                TrackName = trackName,
                                                PlayerName = playerName,
                                                TrackCode = trackCode,

                                            };
                                            //if we use personal best
                                            if (Settings.SelectedComparisonMode.Key == 0)
                                            {
                                                lock (_syncLock)
                                                    //Add reference lap to list
                                                    Settings.CarTrackTelemetries.Add(SelectedCarTrackTelemetry);
                                                
                                            }
                                            else if (Settings.SelectedComparisonMode.Key == 1)
                                            {
                                                CurrentSessionBestTelemetry = SelectedCarTrackTelemetry;
                                                PluginManager.SetPropertyValue("ReferenceLapSet", this.GetType(), true);
                                            }
                                            SetReferenceLap();




                                        }
                                        //if latest lap is faster than reference lap
                                        if (SelectedCarTrackTelemetry.LapTime.TotalSeconds == 0
                                            ||
                                            SelectedCarTrackTelemetry.LapTime.TotalMilliseconds > data.OldData.CurrentLapTime.TotalMilliseconds
                                            )
                                        {
                                            //Update reference lap telemetry
                                            SelectedCarTrackTelemetry.LapTime = data.OldData.CurrentLapTime;
                                            SelectedCarTrackTelemetry.TelemetryDatas = _CurrentLapTelemetry;
                                            //if we use personal best
                                            if (Settings.SelectedComparisonMode.Key == 0)
                                            {
                                                //Save reference lap
                                                this.SaveCommonSettings("GeneralSettings", Settings);
                                                SimHub.Logging.Current.Info("SimRaceX.Telemetry.Comparer : New personal best set");
                                            }
                                            //else if we use session best
                                            else if (Settings.SelectedComparisonMode.Key == 1)
                                            {

                                                //try to get the personal best
                                                var personalBest = GetPersonalBestTelemetry();
                                                //if personal best is null
                                                if (personalBest is null)
                                                {
                                                    lock (_syncLock)
                                                        //Add reference lap to list
                                                        Settings.CarTrackTelemetries.Add(SelectedCarTrackTelemetry);
                                                }
                                                //else if session best is faster that personal best
                                                else if (personalBest.LapTime.TotalSeconds == 0
                                                    ||
                                                    personalBest.LapTime.TotalMilliseconds > data.OldData.CurrentLapTime.TotalMilliseconds)
                                                {
                                                    personalBest.LapTime = data.OldData.CurrentLapTime;
                                                    personalBest.TelemetryDatas = _CurrentLapTelemetry;
                                                    //update reference lap
                                                    this.SaveCommonSettings("GeneralSettings", Settings);
                                                }
                                            }



                                            //if (Settings.SelectedComparisonReference.Key == 0)
                                            //{
                                            //    //Task.Run(() => GetSelectedCarTrackTelemetryDatas(false));
                                            //    SelectedCarTrackTelemetry.LapTime = data.OldData.CurrentLapTime;
                                            //    this.SaveCommonSettings("GeneralSettings", Settings);
                                            //}                                       
                                            pluginManager.SetPropertyValue("ReferenceLapTime", this.GetType(), SelectedCarTrackTelemetry.LapTime);
                                            pluginManager.SetPropertyValue("ReferenceLapPlayerName", this.GetType(), SelectedCarTrackTelemetry.PlayerName);
                                            pluginManager.TriggerEvent("ReferenceLapChanged", this.GetType());
                                        }
                                    }
                                }
                                ResetCurrentLapTelemetry();
                            }
                            else if (_CurrentLapTelemetry is null)
                                ResetCurrentLapTelemetry();

                            //Add data to current lap telemetry
                            if (data.NewData.CurrentLapTime.TotalMilliseconds > 0)
                            {
                                _CurrentLapTelemetry.Add(new TelemetryData
                                {
                                    Throttle = data.NewData.Throttle,
                                    Brake = data.NewData.Brake,
                                    Clutch = data.NewData.Clutch,
                                    LapDistance = data.NewData.CarCoordinates[0],
                                    Speed = data.NewData.SpeedKmh,
                                    Gear = data.NewData.Gear,
                                    SteeringAngle = SteeringAngle,
                                });

                            }
                            //if a reference lap is set
                            if (SelectedCarTrackTelemetry != null && SelectedCarTrackTelemetry.TelemetryDatas.Count > 0)
                            {
                                //get current lap distance percent
                                double lapDistance = data.NewData.CarCoordinates[0];
                                double distance = SelectedCarTrackTelemetry.TelemetryDatas[0].LapDistance - lapDistance;
                                int index = -1;
                                //loop
                                if (lapDistance <= 0.5)
                                {
                                    for (int i = 0; i < SelectedCarTrackTelemetry.TelemetryDatas.Count; i++)
                                        if (SelectedCarTrackTelemetry.TelemetryDatas[i].LapDistance > lapDistance)
                                        {
                                            index = i;
                                            break;
                                        }
                                }
                                //loop reverse (in order to minimize possible number of iterations)
                                else
                                {
                                    for (int i = SelectedCarTrackTelemetry.TelemetryDatas.Count - 1; i >= 0; i--)
                                    {
                                        if (SelectedCarTrackTelemetry.TelemetryDatas[i].LapDistance < lapDistance)
                                        {
                                            index = i;
                                            break;
                                        }
                                    }
                                }
                                //get and display reference lap datas
                                if (index > -1)
                                {
                                    TelemetryData telemetryData = SelectedCarTrackTelemetry.TelemetryDatas[index];
                                    pluginManager.SetPropertyValue("ReferenceLapThrottle", this.GetType(), telemetryData.Throttle);
                                    pluginManager.SetPropertyValue("ReferenceLapBrake", this.GetType(), telemetryData.Brake);
                                    pluginManager.SetPropertyValue("ReferenceLapClutch", this.GetType(), telemetryData.Clutch);
                                    pluginManager.SetPropertyValue("ReferenceLapSpeed", this.GetType(), telemetryData.Speed);
                                    pluginManager.SetPropertyValue("ReferenceLapGear", this.GetType(), telemetryData.Gear);
                                    pluginManager.SetPropertyValue("ReferenceLapSteeringAngle", this.GetType(), telemetryData.SteeringAngle);
                                }
                            }
                        }

                        tickCount = 0;
                    }
                    else
                        tickCount++;

                  
                }
            }
        }
        public void End(PluginManager pluginManager)
        {
            this.SaveCommonSettings("GeneralSettings", Settings);
        }
        public void GetSelectedViewTelemetryDatas(bool getMap)
        {
            if (SelectedViewTelemetry is null)
                return;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ThrottleLineSeries.Clear();
                BrakeLineSeries.Clear();
            });

            foreach (TelemetryData data in SelectedViewTelemetry.TelemetryDatas)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ThrottleLineSeries.Add(new DataPoint(data.LapDistance, data.Throttle));
                    BrakeLineSeries.Add(new DataPoint(data.LapDistance, data.Brake));
                });
               
            }
            //if (getMap)
            //    LoadMap($@"C:\Program Files (x86)\SimHub\PluginsData\IRacing\MapRecords\{_SelectedCarTrackTelemetry.TrackCode}.shtl");

         }       
        void InitSimHubProperties()
        {
            PluginManager.AddProperty("ReferenceLapThrottle", this.GetType(), 0);
            PluginManager.AddProperty("ReferenceLapBrake", this.GetType(), 0);
            PluginManager.AddProperty("ReferenceLapClutch", this.GetType(), 0);
            PluginManager.AddProperty("ReferenceLapSpeed", this.GetType(), 0);
            PluginManager.AddProperty("ReferenceLapGear", this.GetType(), "");
            PluginManager.AddProperty("ReferenceLapTime", this.GetType(), new TimeSpan(0, 0, 0));
            PluginManager.AddProperty("ReferenceLapPlayerName", this.GetType(), "");
            PluginManager.AddProperty("ReferenceLapSteeringAngle", this.GetType(), SteeringAngle);
            PluginManager.AddProperty("ShowBrakeTrace", this.GetType(), Settings.ShowBrakeTrace);
            PluginManager.AddProperty("ShowThrottleTrace", this.GetType(), Settings.ShowThrottleTrace);
            PluginManager.AddProperty("ShowSpeedTrace", this.GetType(), Settings.ShowSpeedTrace);
            PluginManager.AddProperty("ShowGauges", this.GetType(), Settings.ShowGauges);
            PluginManager.AddProperty("ShowSteeringAngle", this.GetType(), Settings.ShowSteeringAngle);
            PluginManager.AddProperty("SelectedComparisonMode", this.GetType(), Settings.SelectedComparisonMode.Value);
            PluginManager.AddProperty("SteeringAngle", this.GetType(), SteeringAngle);
            PluginManager.AddProperty("ReferenceLapSet", this.GetType(), false);
            PluginManager.AddProperty("PersonalBestDiscardInvalidLap", this.GetType(), Settings.PersonalBestDiscardInvalidLap);
            PluginManager.AddProperty("SessionBestDiscardInvalidLap", this.GetType(), Settings.SessionBestDiscardInvalidLap);
        }
        void InitSimHubEvents()
        {
            PluginManager.AddEvent("RefenceLapChanged", this.GetType());
        }
        void InitSimHubActions()
        {
            this.AddAction("ResetCurrentSessionBest", (a, b) =>
            {
                ResetCurrentSessionBest();
            });
            this.AddAction("CycleComparisonReference", (a, b) =>
            {
                CycleComparisonReference();
            });
        }
        public void ResetReferenceLap()
        {
            SelectedCarTrackTelemetry = null;           
            PluginManager.SetPropertyValue("ReferenceLapTime", this.GetType(), new TimeSpan(0, 0, 0));
            PluginManager.SetPropertyValue("ReferenceLapPlayerName", this.GetType(), "");
            PluginManager.AddProperty("ReferenceLapSet", this.GetType(), false);
            PluginManager.TriggerEvent("ReferenceLapChanged", this.GetType());
        }
        //private void SetPropertyChanged()
        //{
        //    foreach (CarTrackTelemetry carTrackTelemetry in Settings.CarTrackTelemetries)
        //        carTrackTelemetry.PropertyChanged += CarTrackTelemetry_PropertyChanged;
        //}
        private void SetReferenceLap()
        {
            if (PluginManager.LastData.NewData is null)
            {
                ResetReferenceLap();
                return;
            }              

            if (PluginManager.LastData.GameName != null 
                && PluginManager.LastData.NewData.CarModel != null 
                && PluginManager.LastData.NewData.TrackId != null)
            {
                switch (Settings.SelectedComparisonMode.Key)
                {
                    case 0:
                        {
                            PersonalBestTelemetry = GetPersonalBestTelemetry();
                            if (PersonalBestTelemetry is null)
                                ResetReferenceLap();
                            else                      
                                SelectedCarTrackTelemetry = PersonalBestTelemetry;  
                        }
                        break;
                    case 1:
                        {
                            if (CurrentSessionBestTelemetry is null && SelectedCarTrackTelemetry != null)
                                ResetReferenceLap();
                            else if (CurrentSessionBestTelemetry != null && SelectedCarTrackTelemetry != CurrentSessionBestTelemetry)
                                SelectedCarTrackTelemetry = CurrentSessionBestTelemetry;
                        }
                        break;
                    case 2:
                        {
                            if (SelectedBestOfFriendTelemetry is null)
                                ResetReferenceLap();
                            else
                                SelectedCarTrackTelemetry = SelectedBestOfFriendTelemetry;
                                                   
                        }
                        break;
                }             
            }
            PluginManager.AddProperty("ReferenceLapSet", this.GetType(), SelectedCarTrackTelemetry != null);

        }
        private CarTrackTelemetry GetPersonalBestTelemetry()
        {
            if (PluginManager.LastData is null)
                return null;
            if (PluginManager.LastData.NewData is null)
                return null;

            string gameName = PluginManager.LastData.GameName;
            string carModel = PluginManager.LastData.NewData.CarModel;
            string playerName = PluginManager.LastData.NewData.PlayerName;
            string trackCode = PluginManager.LastData.NewData.TrackCode;

            return _Settings.CarTrackTelemetries.FirstOrDefault(x =>
                                    x.GameName == gameName
                                    && x.TrackCode == trackCode
                                    && x.CarName == carModel
                                    && x.PlayerName == playerName
                                    );
        }
        public Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            _View = new TelemetryComparerView(this);
            return _View;
        }
        private void UpdateSteeringAngle()
        {
            PluginManager.SetPropertyValue("SteeringAngle", this.GetType(), SteeringAngle);
        }
        void ResetCurrentSessionBest()
        {
            CurrentSessionBestTelemetry = null;
            ResetReferenceLap();
            SimHub.Logging.Current.Info("SimRaceX.Telemetry.Comparer : Current session reference lap has been reset");
        }
        void CycleComparisonReference()
        {
            if (Settings.SelectedComparisonMode.Equals(null))
                Settings.SelectedComparisonMode = Settings.ComparisonModes.First(x => x.Key == 0);
            else if (Settings.SelectedComparisonMode.Key < 2)
                Settings.SelectedComparisonMode = 
                    Settings.ComparisonModes.First(x => x.Key == Settings.SelectedComparisonMode.Key + 1);
            else
                Settings.SelectedComparisonMode = Settings.ComparisonModes.First(x => x.Key == 0);
        }
        void ResetCurrentLapTelemetry()
        {
            _CurrentLapTelemetry = new List<TelemetryData>();
            CurrentLapHasIncidents = false;
        }
        #endregion

        #region Events
        public event EventHandler CurrentSessionChanged;
        private void TelemetryComparerPlugin_CurrentSessionChanged(object sender, EventArgs e)
        {
            SimHub.Logging.Current.Info("SimRaceX.Telemetry.Comparer : Current session has changed");
            IncidentCount = 0;
            CurrentLapHasIncidents = false;
            IsInpit = 1;

            if (Settings.SelectedComparisonMode.Key == 0)
                SetReferenceLap();
            else if (Settings.SelectedComparisonMode.Key == 1)
                ResetCurrentSessionBest();
            else if (Settings.SelectedComparisonMode.Key == 2)
            {
                OnPropertyChanged(nameof(AvailableBestOfFriendTelemetries));
                OnPropertyChanged(nameof(SelectedBestOfFriendTelemetry));
            }
            OnPropertyChanged(nameof(FilteredTelemetries));
        }
        public event EventHandler IncidentCountChanged;
        private void TelemetryComparerPlugin_IncidentCountChanged(object sender, EventArgs e)
        {
            //if (IncidentCount > 0 && _CurrentLapTelemetry != null)
            //    CurrentLapHasIncidents = true;         
        }
        public event EventHandler SelectedCarTrackTelemetryChanged;
        private void TelemetryComparerPlugin_SelectedCarTrackTelemetryChanged(object sender, EventArgs e)
        {
            if (SelectedCarTrackTelemetry != null)
            {
                PluginManager.SetPropertyValue("ReferenceLapTime", this.GetType(), SelectedCarTrackTelemetry.LapTime);
                PluginManager.SetPropertyValue("ReferenceLapPlayerName", this.GetType(), SelectedCarTrackTelemetry.PlayerName);
                PluginManager.SetPropertyValue("ReferenceLapSet", this.GetType(), true);
                PluginManager.TriggerEvent("ReferenceLapChanged", this.GetType());
            }
            else
                PluginManager.SetPropertyValue("ReferenceLapSet", this.GetType(), false);
        }
        public event EventHandler IsInPitChanged;
        private void TelemetryComparerPlugin_IsInPitChanged(object sender, EventArgs e)
        {
            if (IsInpit == 1)            
                ResetReferenceLap();
            else            
                ResetCurrentLapTelemetry(); 
        }

        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ShowBrakeTrace")
                PluginManager.SetPropertyValue("ShowBrakeTrace", this.GetType(), Settings.ShowBrakeTrace);
            else if (e.PropertyName == "ShowThrottleTrace")
                PluginManager.SetPropertyValue("ShowThrottleTrace", this.GetType(), Settings.ShowThrottleTrace);
            else if (e.PropertyName == "ShowSpeedTrace")
                PluginManager.SetPropertyValue("ShowSpeedTrace", this.GetType(), Settings.ShowSpeedTrace);
            else if (e.PropertyName == "ShowGauges")
            {
                PluginManager.SetPropertyValue("ShowGauges", this.GetType(), Settings.ShowGauges);
                Settings.ShowSteeringAngle = Settings.ShowGauges;
            }               
            else if (e.PropertyName == "ShowSteeringAngle")
                PluginManager.SetPropertyValue("ShowSteeringAngle", this.GetType(), Settings.ShowSteeringAngle);
        }
        private void Settings_SelectedComparisonModeChanged(object sender, EventArgs e)
        {
            SimHub.Logging.Current.Info($"SimRaceX.Telemetry.Comparer : Comparison reference is set to '{Settings.SelectedComparisonMode.Value}'");
            PluginManager.SetPropertyValue("SelectedComparisonMode", this.GetType(), Settings.SelectedComparisonMode.Value);
        

            OnPropertyChanged(nameof(IsPersonalBestMode));
            OnPropertyChanged(nameof(IsSessionBestMode));
            OnPropertyChanged(nameof(IsBestOfFriendMode));

            if (Settings.SelectedComparisonMode.Key == 2)
            {
                OnPropertyChanged(nameof(AvailableBestOfFriendTelemetries));
                OnPropertyChanged(nameof(SelectedBestOfFriendTelemetry));   
            }              

            SetReferenceLap();
        }

        private void CarTrackTelemetry_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            //if (e.PropertyName.Equals("UseAsReferenceLap") && (sender as CarTrackTelemetry).UseAsReferenceLap)
            //{
            //    var item = sender as CarTrackTelemetry;
            //    foreach (CarTrackTelemetry carTrackTelemetry in Settings.CarTrackTelemetries.Where(
            //        x => x.GameName == item.GameName && x.TrackCode == item.TrackCode && x.CarName == item.CarName))
            //    {
            //        if (carTrackTelemetry != item)
            //            carTrackTelemetry.UseAsReferenceLap = false;
            //    }
            //    this.SaveCommonSettings("GeneralSettings", Settings);
            //}
        }
        private void CarTrackTelemetries_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(FilteredTelemetries));
        }
        #endregion

        //private void LoadMap(string file)
        //{

        //    DataRecord dataRecord = JsonExtensions.FromJsonGZipFile<DataRecord>(file);

        //    _View.mapCanvas.Children.Clear();
        //    int num2 = -1;
        //    Polyline polyline = null;
        //    PointCollection pointCollection = null;
        //    double num3 = dataRecord.CarCoordinates.Min((PositionItem i) => i.Value[0]);
        //    double num4 = dataRecord.CarCoordinates.Min((PositionItem i) => i.Value[2]);
        //    double num5 = dataRecord.CarCoordinates.Max((PositionItem i) => i.Value[0]);
        //    double num6 = dataRecord.CarCoordinates.Max((PositionItem i) => i.Value[2]);
        //    foreach (PositionItem carCoordinate in dataRecord.CarCoordinates)
        //    {
        //        if (polyline == null || num2 != carCoordinate.CurrentSector)
        //        {
        //            if (polyline != null)
        //            {
        //                pointCollection.Add(new Point(carCoordinate.Value[0] - num3 + 200.0, carCoordinate.Value[2] - num4 + 200.0));
        //                _View.mapCanvas.Children.Add(polyline);
        //            }
        //            polyline = new Polyline();
        //            polyline.StrokeEndLineCap = PenLineCap.Round;
        //            polyline.Stroke = new SolidColorBrush((carCoordinate.CurrentSector % 2 == 1) ? Colors.Red : Colors.Orange);
        //            polyline.StrokeThickness = 10;
        //            pointCollection = (polyline.Points = new PointCollection());
        //        }
        //        pointCollection.Add(new Point(carCoordinate.Value[0] - num3 + 200.0, carCoordinate.Value[2] - num4 + 200.0));
        //        num2 = carCoordinate.CurrentSector;
        //    }
        //    if (polyline.Points.Any())
        //    {
        //        _View.mapCanvas.Children.Add(polyline);
        //    }
        //    for (int j = 0; j < dataRecord.CarCoordinates.Count - 1; j++)
        //    {
        //        PositionItem positionItem = dataRecord.CarCoordinates[j];
        //        double distanceBetweenPoints = DataRecordBase.GetDistanceBetweenPoints(dataRecord.CarCoordinates[j], dataRecord.CarCoordinates[j + 1]);
        //        if (distanceBetweenPoints > 15.0)
        //        {
        //            Ellipse element = new Ellipse
        //            {
        //                Width = 4.0,
        //                Height = 4.0,
        //                Stroke = new SolidColorBrush(Colors.LightBlue)
        //            };
        //            Canvas.SetLeft(element, positionItem.Value[0] - num3 + 200.0 - 2.0);
        //            Canvas.SetTop(element, positionItem.Value[2] - num4 + 200.0 - 2.0);
        //            Ellipse ellipse = new Ellipse
        //            {
        //                Width = 30.0,
        //                Height = 30.0,
        //                Stroke = new SolidColorBrush(Colors.LightBlue),
        //                Fill = new SolidColorBrush(Colors.Transparent)
        //            };
        //            Canvas.SetLeft(ellipse, positionItem.Value[0] - num3 + 200.0 - 15.0);
        //            Canvas.SetTop(ellipse, positionItem.Value[2] - num4 + 200.0 - 15.0);
        //            ellipse.ToolTip = distanceBetweenPoints.ToString("0.00");
        //            _View.mapCanvas.Children.Add(element);
        //            _View.mapCanvas.Children.Add(ellipse);
        //            positionItem = dataRecord.CarCoordinates[j + 1];
        //            Ellipse element2 = new Ellipse
        //            {
        //                Width = 4.0,
        //                Height = 4.0,
        //                Stroke = new SolidColorBrush(Colors.LightBlue)
        //            };
        //            Canvas.SetLeft(element2, positionItem.Value[0] - num3 + 200.0 - 2.0);
        //            Canvas.SetTop(element2, positionItem.Value[2] - num4 + 200.0 - 2.0);
        //            _View.mapCanvas.Children.Add(element2);
        //        }
        //        else
        //        {
        //            Ellipse element3 = new Ellipse
        //            {
        //                Width = 1.0,
        //                Height = 1.0,
        //                Stroke = new SolidColorBrush(Colors.Green)
        //            };
        //            Canvas.SetLeft(element3, positionItem.Value[0] - num3 + 200.0 - 0.5);
        //            Canvas.SetTop(element3, positionItem.Value[2] - num4 + 200.0 - 0.5);
        //            _View.mapCanvas.Children.Add(element3);
        //        }
        //    }
        //    double width = num5 - num3 + 400.0;
        //    double height = num6 - num4 + 400.0;
        //    if (height > width)
        //    {
        //        _View.mapCanvas.RenderTransformOrigin = new Point(0.5, 0.5);
        //        RotateTransform myRotateTransform = new RotateTransform();
        //        myRotateTransform.Angle = 90;
        //        TransformGroup myTransformGroup = new TransformGroup();
        //        myTransformGroup.Children.Add(myRotateTransform);
        //        _View.mapCanvas.RenderTransform = myTransformGroup;

        //    }



        //    //_View.mapCanvas.Width = num5 - num3 + 400.0;
        //    //_View.mapCanvas.Height = num6 - num4 + 400.0;
        //    _View.ZoomBorder.UpdateLayout();
        //    _View.ZoomBorder.AutoFit();
        //}

    }
}
