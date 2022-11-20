using GameReaderCommon;
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
        private PlotModel _TelemetryPlotModel;
        private ObservableCollection<DataPoint> _ThrottleLineSeries;
        private ObservableCollection<DataPoint> _BrakeLineSeries;
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
                if (_SelectedCarTrackTelemetry != null)
                {
                    GetSelectedCarTrackTelemetryDatas(true);
                }
                OnPropertyChanged(nameof(SelectedCarTrackTelemetry)); 
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
                    SelectedCarTrackTelemetry = null;

                if (carTrackTelemetry.PlayerName != PluginManager.LastData.NewData.PlayerName)
                {
                    var item = Settings.CarTrackTelemetries.FirstOrDefault(
                    x => x.GameName == carTrackTelemetry.GameName 
                    && x.TrackCode == carTrackTelemetry.TrackCode 
                    && x.CarName == carTrackTelemetry.CarName
                    && x.PlayerName == PluginManager.LastData.NewData.PlayerName
                    );
                    if (item != null)
                    {
                        item.UseAsReferenceLap = true;
                        SelectedCarTrackTelemetry = item;
                    }
                }
                else
                {
                    PluginManager.SetPropertyValue("ReferenceLapTime", this.GetType(), new TimeSpan(0, 0, 0));
                    PluginManager.SetPropertyValue("ReferenceLapPlayerName", this.GetType(), "");
                    PluginManager.TriggerEvent("ReferenceLapChanged", this.GetType());                    
                }
               
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
            Settings.PropertyChanged += Settings_PropertyChanged;

          
            ThrottleLineSeries = new ObservableCollection<DataPoint>();
            BrakeLineSeries = new ObservableCollection<DataPoint>();

            BindingOperations.EnableCollectionSynchronization(Settings.CarTrackTelemetries, _syncLock);

            pluginManager.AddProperty("ReferenceLapThrottle", this.GetType(), 0);
            pluginManager.AddProperty("ReferenceLapBrake", this.GetType(), 0);
            pluginManager.AddProperty("ReferenceLapClutch", this.GetType(), 0);
            pluginManager.AddProperty("ReferenceLapSpeed", this.GetType(), 0);
            pluginManager.AddProperty("ReferenceLapGear", this.GetType(), "");
            pluginManager.AddProperty("ReferenceLapTime", this.GetType(), new TimeSpan(0,0,0));
            pluginManager.AddProperty("ReferenceLapPlayerName", this.GetType(), "");
            pluginManager.AddProperty("ShowBrakeTrace", this.GetType(), Settings.ShowBrakeTrace);
            pluginManager.AddProperty("ShowThrottleTrace", this.GetType(), Settings.ShowThrottleTrace);
            pluginManager.AddProperty("ShowSpeedTrace", this.GetType(), Settings.ShowSpeedTrace);
            pluginManager.AddProperty("ShowGauges", this.GetType(), Settings.ShowGauges);



            pluginManager.AddEvent("RefenceLapChanged", this.GetType());

            Settings.CarTrackTelemetries.CollectionChanged += CarTrackTelemetries_CollectionChanged;
            SetPropertyChanged();



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
                PluginManager.SetPropertyValue("ShowGauges", this.GetType(), Settings.ShowGauges);
        }

        public Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            _View = new TelemetryComparerView(this);
            return _View;
        }
        public void End(PluginManager pluginManager)
        {
            this.SaveCommonSettings("GeneralSettings", Settings);
        }
        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
          

            if (!data.GameRunning)
            {
                //if (_SelectedCarTrackTelemetry != null)
                //    SelectedCarTrackTelemetry = null;
                return;
            }

            if (data.GameRunning && !data.NewData.Spectating && data.OldData != null)
            {
                string gameName = data.GameName;
                string carModel = data.NewData.CarModel;
                string trackName = data.NewData.TrackName;
                string playerName = data.NewData.PlayerName;
                string trackCode = data.NewData.TrackCode;

                if (data.GameName != null && data.NewData.CarModel != null && data.NewData.TrackId != null)
                {

                    if (SelectedCarTrackTelemetry is null)
                    {
                        _SelectedCarTrackTelemetry = _Settings.CarTrackTelemetries.FirstOrDefault(x =>
                            x.GameName == gameName
                            && x.TrackCode == trackCode
                            && x.CarName == carModel
                            && x.UseAsReferenceLap
                            );
                        if (SelectedCarTrackTelemetry != null)
                        {
                            pluginManager.SetPropertyValue("ReferenceLapTime", this.GetType(), SelectedCarTrackTelemetry.LapTime);
                            pluginManager.SetPropertyValue("ReferenceLapPlayerName", this.GetType(), SelectedCarTrackTelemetry.PlayerName);
                            pluginManager.TriggerEvent("ReferenceLapChanged", this.GetType());
                        }
                    }

                    if (data.OldData.CurrentLap != data.NewData.CurrentLap || tickCount == (SimHub.Licensing.LicenseManager.IsValid ? 3 : 0))
                    {
                        if (SelectedCarTrackTelemetry != null
                          &&
                          (
                          SelectedCarTrackTelemetry.GameName != gameName
                          || SelectedCarTrackTelemetry.CarName != carModel
                          || SelectedCarTrackTelemetry.TrackCode != trackCode
                          || SelectedCarTrackTelemetry.UseAsReferenceLap == false
                          )
                        )
                        {
                            _SelectedCarTrackTelemetry = _Settings.CarTrackTelemetries.FirstOrDefault(x =>
                            x.GameName == gameName
                            && x.TrackCode == trackCode
                            && x.CarName == carModel
                            && x.UseAsReferenceLap
                            );
                            if (_SelectedCarTrackTelemetry != null)
                            {
                                pluginManager.SetPropertyValue("ReferenceLapTime", this.GetType(), SelectedCarTrackTelemetry.LapTime);
                                pluginManager.SetPropertyValue("ReferenceLapPlayerName", this.GetType(), SelectedCarTrackTelemetry.PlayerName);
                                pluginManager.TriggerEvent("ReferenceLapChanged", this.GetType());
                            }
                       
                        }

                        if (data.OldData.CurrentLap != data.NewData.CurrentLap)
                        {
                            if (_CurrentLapTelemetry != null && _CurrentLapTelemetry.Count > 0)
                            {
                                double firstDataDistance = _CurrentLapTelemetry.First().LapDistance;
                                double lastDataDistance = _CurrentLapTelemetry.Last().LapDistance;

                                if (firstDataDistance < 0.1 && lastDataDistance > 0.9 && data.OldData.IsLapValid)
                                {
                                    if (SelectedCarTrackTelemetry is null)
                                    {
                                        _SelectedCarTrackTelemetry = new CarTrackTelemetry
                                        {
                                            GameName = gameName,
                                            CarName = carModel,
                                            TrackName = trackName,
                                            PlayerName = playerName,
                                            TrackCode = trackCode,
                                            UseAsReferenceLap = true
                                        };
                                        lock (_syncLock)
                                        {
                                            Settings.CarTrackTelemetries.Add(SelectedCarTrackTelemetry);
                                        }
                                    }

                                    if (SelectedCarTrackTelemetry.LapTime.TotalSeconds == 0
                                        ||
                                        SelectedCarTrackTelemetry.LapTime.TotalMilliseconds > data.OldData.CurrentLapTime.TotalMilliseconds
                                        )
                                    {
                                        SelectedCarTrackTelemetry.TelemetryDatas = _CurrentLapTelemetry;
                                        GetSelectedCarTrackTelemetryDatas(false);
                                        SelectedCarTrackTelemetry.LapTime = data.OldData.CurrentLapTime;
                                        pluginManager.SetPropertyValue("ReferenceLapTime", this.GetType(), SelectedCarTrackTelemetry.LapTime);
                                        pluginManager.SetPropertyValue("ReferenceLapPlayerName", this.GetType(), SelectedCarTrackTelemetry.PlayerName);

                                        this.SaveCommonSettings("GeneralSettings", Settings);
                                        pluginManager.TriggerEvent("ReferenceLapChanged", this.GetType());
                                    }
                                }
                            }
                            _CurrentLapTelemetry = new List<TelemetryData>();
                        }
                        else if (_CurrentLapTelemetry is null)
                            _CurrentLapTelemetry = new List<TelemetryData>();

                        if (data.NewData.CurrentLapTime.TotalSeconds > 0)
                        {
                            _CurrentLapTelemetry.Add(new TelemetryData
                            {
                                Throttle = data.NewData.Throttle,
                                Brake = data.NewData.Brake,
                                Clutch = data.NewData.Clutch,
                                LapDistance = data.NewData.CarCoordinates[0],
                                Speed = data.NewData.SpeedKmh,
                                Gear = data.NewData.Gear
                            });

                        }
                        if (SelectedCarTrackTelemetry != null && SelectedCarTrackTelemetry.TelemetryDatas.Count > 0)
                        {
                            double lapDistance = data.NewData.CarCoordinates[0];
                            double distance = SelectedCarTrackTelemetry.TelemetryDatas[0].LapDistance - lapDistance;
                            int index = -1;

                            if (lapDistance <= 0.5)
                            {
                                for (int i = 0; i < SelectedCarTrackTelemetry.TelemetryDatas.Count; i++)                                
                                    if (SelectedCarTrackTelemetry.TelemetryDatas[i].LapDistance > lapDistance)
                                    {
                                        index = i;
                                        break;
                                    }
                            }
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
                            if (index > -1)
                            {
                                TelemetryData telemetryData = SelectedCarTrackTelemetry.TelemetryDatas[index];
                                pluginManager.SetPropertyValue("ReferenceLapThrottle", this.GetType(), telemetryData.Throttle);
                                pluginManager.SetPropertyValue("ReferenceLapBrake", this.GetType(), telemetryData.Brake);
                                pluginManager.SetPropertyValue("ReferenceLapClutch", this.GetType(), telemetryData.Clutch);                              
                                pluginManager.SetPropertyValue("ReferenceLapSpeed", this.GetType(), telemetryData.Speed);                              
                                pluginManager.SetPropertyValue("ReferenceLapGear", this.GetType(), telemetryData.Gear);                              
                            }
                        }

                        tickCount = 0;
                    }
                    else
                        tickCount++;
                }
            }
        }
        public void GetSelectedCarTrackTelemetryDatas(bool getMap)
        {
          

            if (_SelectedCarTrackTelemetry is null)
                return;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ThrottleLineSeries.Clear();
                BrakeLineSeries.Clear();
            });

            foreach (TelemetryData data in _SelectedCarTrackTelemetry.TelemetryDatas)
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
        private void SetPropertyChanged()
        {
            foreach (CarTrackTelemetry carTrackTelemetry in Settings.CarTrackTelemetries)
                carTrackTelemetry.PropertyChanged += CarTrackTelemetry_PropertyChanged;
        }

        private void CarTrackTelemetry_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("UseAsReferenceLap") && (sender as CarTrackTelemetry).UseAsReferenceLap)
            {
                var item = sender as CarTrackTelemetry;
                foreach(CarTrackTelemetry carTrackTelemetry in Settings.CarTrackTelemetries.Where(
                    x=>x.GameName == item.GameName && x.TrackCode == item.TrackCode && x.CarName == item.CarName))
                {
                    if (carTrackTelemetry != item)
                        carTrackTelemetry.UseAsReferenceLap = false;
                }
                this.SaveCommonSettings("GeneralSettings", Settings);
            }
        }
        private void CarTrackTelemetries_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            SetPropertyChanged();
        }
        #endregion

    }
}
