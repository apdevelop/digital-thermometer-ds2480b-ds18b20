namespace DigitalThermometer.App.Utils
{
    using System;
    using System.Windows.Input;

    // http://www.c-sharpcorner.com/UploadFile/1a81c5/a-simple-silverlight-application-implementing-mvvm2/

    public class RelayCommand : ICommand
    {
        private Func<bool> canExecute;
        private Action<object> executeAction;
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

        public void RaiseCanExecuteChanged()
        {
            if (CanExecuteChanged != null)
            {
                CanExecuteChanged(this, EventArgs.Empty);
            }
        }

        public bool CanExecute(object parameter)
        {
            return canExecute == null ? true : canExecute();
        }

        public void Execute(object parameter)
        {
            this.executeAction(parameter);
        }
    }
}
