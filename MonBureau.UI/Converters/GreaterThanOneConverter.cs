using System;
using System.Globalization;
using System.Windows.Data;

namespace MonBureau.UI.Converters
{
    /// <summary>
    /// Converts integer to boolean (true if greater than 1)
    /// Used for pagination button enabling
    /// </summary>
    public class GreaterThanOneConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                return intValue > 1;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}