using System;
using System.Windows.Input;

namespace DigitalThermometer.App.Utils
{
    // http://www.c-sharpcorner.com/UploadFile/1a81c5/a-simple-silverlight-application-implementing-mvvm2/

    public class RelayCommand : ICommand
    {
        private readonly Func<bool> canExecute;
       
        private readonly Action<object> executeAction;
      
        public event EventHandler CanExecuteChanged;

        public RelayCommand(Action<object> executeAction, Func<bool> canExecute)
        {
            this.executeAction = executeAction;
            this.canExecute = canExecute;
        }

        public RelayCommand(Action<object> executeAction)
        {
            this.executeAction = executeAction;
            this.canExecute = () => true;
        }

        public void RaiseCanExecuteChanged() => this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);

        public bool CanExecute(object parameter) => this.canExecute == null ? true : this.canExecute();

        public void Execute(object parameter) => this.executeAction(parameter);
    }
}
