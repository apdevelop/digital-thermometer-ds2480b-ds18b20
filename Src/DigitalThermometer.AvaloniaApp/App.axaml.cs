using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using DigitalThermometer.AvaloniaApp.ViewModels;
using DigitalThermometer.AvaloniaApp.Views;

namespace DigitalThermometer.AvaloniaApp
{
    public class App : Application
    {
        internal static readonly Utils.LocalizationUtil Locale = new Utils.LocalizationUtil(); // TODO: remove global variable

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
                desktop.MainWindow.DataContext = new MainWindowViewModel(desktop.MainWindow);
            }

            base.OnFrameworkInitializationCompleted();

            App.Locale.SetDefaultLanguage(this, "en-US"); // TODO: ? switching
        }
    }
}