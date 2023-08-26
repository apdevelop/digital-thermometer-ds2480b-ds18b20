using Avalonia.Controls;
using Avalonia.Styling;
using System;
using System.Globalization;

namespace DigitalThermometer.AvaloniaApp.Utils
{
    class LocalizationUtil
    {
        // Based on article
        // https://www.c-sharpcorner.com/article/dynamic-localization-in-wpf/

        private IResourceProvider languageDictionary;

        /// <summary>  
        /// Set language based on previously save language setting,  
        /// otherwise set to OS language  
        /// </summary>  
        /// <param name="element"></param>  
        public void SetDefaultLanguage(App element, string cultureName = null)
        {
            languageDictionary = element.Resources.MergedDictionaries[2]; // TODO: ! find i18n
            ////var path = GetDictionaryFileName("App", cultureName != null ? cultureName : CultureInfo.CurrentUICulture.Name);
            ////SetLanguageResourceDictionary(element, path);
        }

        public string this[string key]
        {
            get
            {
                languageDictionary.TryGetResource(key, ThemeVariant.Default, out object res);
                return res as string;
            }
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
        private void SetLanguageResourceDictionary(App element, string dictionaryFileName)
        {
            // TODO: Load ResourceDictionary from resources
            ////languageDictionary = new ResourceDictionary();
            ////languageDictionary.Source = new Uri($"/DigitalThermometer.AvaloniaApp;component/Resources/i18n/" + dictionaryFileName, UriKind.Relative);

            // Check any previous Localization dictionaries loaded  
            var langDictId = -1;
            for (var i = 0; i < element.Resources.MergedDictionaries.Count; i++)
            {
                // Make sure your Localization ResourceDictionarys have the ResourceDictionaryName key
                if (element.Resources.MergedDictionaries[i].TryGetResource("ResourceDictionaryName", ThemeVariant.Default, out var name))
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
