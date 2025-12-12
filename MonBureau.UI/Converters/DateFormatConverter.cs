using System;
using System.Globalization;
using System.Windows.Data;

namespace MonBureau.UI.Converters
{
    public class DateFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateTime)
            {
                var format = parameter?.ToString() ?? "dd/MM/yyyy";
                return dateTime.ToString(format);
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string dateString && DateTime.TryParse(dateString, out var result))
            {
                return result;
            }

            return DateTime.Now;
        }
    }
}