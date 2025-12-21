using System.Globalization;

namespace MonBureau.UI.Services
{
    /// <summary>
    /// Simple static localization helper for XAML binding
    /// Usage: Text="{Binding Source={x:Static loc:Loc.Instance}, Path=[Dashboard_Title]}"
    /// </summary>
    public class Loc
    {
        private static Loc? _instance;
        public static Loc Instance => _instance ??= new Loc();

        private readonly System.Resources.ResourceManager _resourceManager;

        private Loc()
        {
            _resourceManager = Resources.Localization.Strings.ResourceManager;

            // Subscribe to language changes
            LocalizationService.Instance.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(LocalizationService.CurrentLanguage))
                {
                    // Notify all properties changed
                    System.ComponentModel.PropertyChangedEventManager.AddHandler(
                        (System.ComponentModel.INotifyPropertyChanged)this,
                        (sender, args) => { },
                        string.Empty);
                }
            };
        }

        // Indexer for XAML binding
        public string this[string key]
        {
            get
            {
                try
                {
                    var value = _resourceManager.GetString(key, CultureInfo.CurrentUICulture);
                    return value ?? $"[{key}]";
                }
                catch
                {
                    return $"[{key}]";
                }
            }
        }
    }
}