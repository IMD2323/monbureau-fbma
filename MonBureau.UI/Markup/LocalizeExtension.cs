using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace MonBureau.UI.Markup
{
    /// <summary>
    /// Markup extension for localized strings that updates when language changes
    /// Usage: Text="{loc:Localize Dashboard_Title}"
    /// </summary>
    [MarkupExtensionReturnType(typeof(BindingExpression))]
    public class LocalizeExtension : MarkupExtension
    {
        [ConstructorArgument("key")]
        public string Key { get; set; }

        public LocalizeExtension()
        {
            Key = string.Empty;
        }

        public LocalizeExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key))
                return "[Missing Key]";

            // Create a binding to LocalizationManager
            var binding = new Binding("Value")
            {
                Source = new LocalizationProxy(Key),
                Mode = BindingMode.OneWay
            };

            if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget target)
            {
                if (target.TargetObject is DependencyObject)
                {
                    return binding.ProvideValue(serviceProvider);
                }
            }

            return binding;
        }
    }

    /// <summary>
    /// Proxy class that notifies when language changes
    /// </summary>
    public class LocalizationProxy : INotifyPropertyChanged
    {
        private readonly string _key;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Value
        {
            get
            {
                try
                {
                    var resourceManager = Resources.Localization.Strings.ResourceManager;
                    var value = resourceManager.GetString(_key, CultureInfo.CurrentUICulture);
                    return value ?? $"[{_key}]";
                }
                catch
                {
                    return $"[{_key}]";
                }
            }
        }

        public LocalizationProxy(string key)
        {
            _key = key;

            // Subscribe to language changes
            Services.LocalizationService.Instance.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Services.LocalizationService.CurrentLanguage))
                {
                    OnPropertyChanged(nameof(Value));
                }
            };
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}