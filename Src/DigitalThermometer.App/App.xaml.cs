using System;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;

namespace DigitalThermometer.App
{
    public partial class App : Application
    {
        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += this.CurrentDomainUnhandledException;
            this.DispatcherUnhandledException += this.AppDispatcherUnhandledException;

            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var resourceName = "DigitalThermometer.App.Resources.Assembly." + new AssemblyName(args.Name).Name + ".dll";
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        var assemblyData = new byte[stream.Length];
                        stream.Read(assemblyData, 0, assemblyData.Length);
                        return Assembly.Load(assemblyData);
                    }
                    else
                    {
                        return null;
                    }
                }
            };
        }

        protected override void OnStartup(StartupEventArgs e)
        {
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