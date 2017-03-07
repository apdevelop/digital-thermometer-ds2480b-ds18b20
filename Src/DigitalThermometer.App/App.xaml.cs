using System;
using System.Windows;
using System.Windows.Threading;

namespace DigitalThermometer.App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;
            this.DispatcherUnhandledException += AppDispatcherUnhandledException;
           
            base.OnStartup(e);

            (new Views.MainWindow { DataContext = new ViewModels.MainViewModel() }).Show();
        }

        private void CurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            if (exception != null)
            {
                MessageBox.Show(exception.ToString(), "Unhandled exception (CurrentDomain)", MessageBoxButton.OK, MessageBoxImage.Stop);
            }
            else
            {
                MessageBox.Show("Unhandled exception (CurrentDomain)", "Unhandled exception (CurrentDomain)", MessageBoxButton.OK, MessageBoxImage.Stop);
            }

            Environment.Exit(1);
        }

        private void AppDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(e.Exception.ToString(), "Unhandled exception (AppDispatcher)", MessageBoxButton.OK, MessageBoxImage.Stop);
            e.Handled = true;

            Environment.Exit(1);
        }
    }
}
