using OxyPlot;
using SimRaceX.Telemetry.Comparer.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SimRaceX.Telemetry.Comparer.View
{
    /// <summary>
    /// Logique d'interaction pour iRacingWrapper.xaml
    /// </summary>
    public partial class TelemetryComparerView : UserControl
    {
        public TelemetryComparerPlugin Plugin { get; }

        public TelemetryComparerView()
        {
            InitializeComponent();
        }
        public TelemetryComparerView(TelemetryComparerPlugin plugin) : this()
        {
            this.Plugin = plugin;
            this.DataContext = Plugin;
        }

        private void plot_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
           // var test = plot.DefaultTrackerTemplate.LoadContent();
           
        }


    }
}
