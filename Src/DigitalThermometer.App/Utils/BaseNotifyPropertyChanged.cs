namespace DigitalThermometer.App.Utils
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;

    public abstract class BaseNotifyPropertyChanged : INotifyPropertyChanged
    {
        // https://joshsmithonwpf.wordpress.com/2007/08/29/a-base-class-which-implements-inotifypropertychanged/
        // http://stackoverflow.com/questions/9077106/inheriting-from-one-baseclass-that-implements-inotifypropertychanged

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(string propertyName)
        {
            this.VerifyProperty(propertyName);

            var propertyChanged = this.PropertyChanged;
            if (propertyChanged != null)
            {
                propertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        [Conditional("DEBUG")]
        private void VerifyProperty(string propertyName)
        {
            const string paramName = "propertyName";

            if (propertyName == null)
            {
                throw new ArgumentNullException(paramName);
            }

            if (!String.IsNullOrEmpty(propertyName))
            {
                var type = this.GetType();
                var propInfo = type.GetProperty(propertyName);

                if (propInfo == null)
                {
                    throw new ArgumentException(String.Format("Property '{0}' was not found in '{1}' class", propertyName, type.FullName), paramName);
                }
            }
        }
    }
}
