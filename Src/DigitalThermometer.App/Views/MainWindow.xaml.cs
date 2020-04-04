using System.Windows;
using System.Windows.Media;

namespace DigitalThermometer.App.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        }
    }
}