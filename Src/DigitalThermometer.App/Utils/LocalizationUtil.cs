using System;
using System.Globalization;
using System.Threading;
using System.Windows;

namespace DigitalThermometer.App.Utils
{
    class LocalizationUtil
    {
        // Based on article
        // https://www.c-sharpcorner.com/article/dynamic-localization-in-wpf/

        private ResourceDictionary languageDictionary;

        /// <summary>  
        /// Set language based on previously save language setting,  
        /// otherwise set to OS lanaguage  
        /// </summary>  
        /// <param name="element"></param>  
        public void SetDefaultLanguage(FrameworkElement element, string cultureName = null)
        {
            var path = GetDictionaryFileName(GetElementName(element), cultureName != null ? cultureName : CultureInfo.CurrentUICulture.Name);
            SetLanguageResourceDictionary(element, path);
        }

        /// <summary>  
        /// Dynamically switches localization language
        /// </summary>  
        public void SwitchLanguage(FrameworkElement element, string cultureName)
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(cultureName);
            var path = GetDictionaryFileName(GetElementName(element), cultureName);
            SetLanguageResourceDictionary(element, path);
        }

        public string GetValue(string key)
        {
            return (string)languageDictionary[key];
        }

        public string this[string key]
        {
            get
            {
                return (string)languageDictionary[key];
            }
        }

        /// <summary>  
        /// Generate a name from an element base on its class name  
        /// </summary>  
        /// <param name="element"></param>  
        /// <returns></returns>  
        private string GetElementName(FrameworkElement element)
        {
            var elementType = element.GetType().ToString();
            var elementNames = elementType.Split('.');
            var elementName = String.Empty;
            if (elementNames.Length >= 2)
            {
                elementName = elementNames[elementNames.Length - 1];
            }

            return elementName;
        }

        /// <summary>  
        /// Returns the path to the ResourceDictionary file based on the language character string.  
        /// </summary>  
        /// <param name="cultureName">Culture name</param>  
        /// <returns>Dictionary file name</returns>  
        private static string GetDictionaryFileName(string element, string cultureName) => element + "." + cultureName + ".xaml";

        /// <summary>  
        /// Sets or replaces the ResourceDictionary by dynamically loading  
        /// a Localization ResourceDictionary from the file path passed in.  
        /// </summary>  
        /// <param name="dictionaryFileName">Dictionary file name</param>  
        private void SetLanguageResourceDictionary(FrameworkElement element, string dictionaryFileName)
        {
            // Load ResourceDictionary from resources
            languageDictionary = new ResourceDictionary();
            languageDictionary.Source = new Uri($"/DigitalThermometer.App;component/Resources/i18n/" + dictionaryFileName, UriKind.Relative);

            // Check any previous Localization dictionaries loaded  
            var langDictId = -1;
            for (var i = 0; i < element.Resources.MergedDictionaries.Count; i++)
            {
                // Make sure your Localization ResourceDictionarys have the ResourceDictionaryName key
                if (element.Resources.MergedDictionaries[i].Contains("ResourceDictionaryName"))
                {
                    langDictId = i;
                    break;
                }
            }

            if (langDictId == -1)
            {
                // Add in newly loaded Resource Dictionary  
                element.Resources.MergedDictionaries.Add(languageDictionary);
            }
            else
            {
                // Replace the current langage dictionary with the new one  
                element.Resources.MergedDictionaries[langDictId] = languageDictionary;
            }
        }
    }
}
