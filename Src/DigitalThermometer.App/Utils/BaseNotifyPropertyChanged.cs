using System;
using System.ComponentModel;
using System.Diagnostics;

namespace DigitalThermometer.App.Utils
{
    public abstract class BaseNotifyPropertyChanged : INotifyPropertyChanged
    {
        // https://joshsmithonwpf.wordpress.com/2007/08/29/a-base-class-which-implements-inotifypropertychanged/
        // http://stackoverflow.com/questions/9077106/inheriting-from-one-baseclass-that-implements-inotifypropertychanged

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(string propertyName)
        {
            this.VerifyProperty(propertyName);

            var propertyChanged = this.PropertyChanged;
            propertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        [Conditional("DEBUG")]
        private void VerifyProperty(string propertyName)
        {
            if (propertyName == null)
            {
                throw new ArgumentNullException(nameof(propertyName));
            }

            if (!String.IsNullOrEmpty(propertyName))
            {
                var type = this.GetType();
                var propInfo = type.GetProperty(propertyName);
                if (propInfo == null)
                {
                    throw new ArgumentException($"Property '{propertyName}' was not found in '{type.FullName}' class", nameof(propertyName));
                }
            }
        }
    }
}
