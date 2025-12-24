// MonBureau.UI/Converters/LessThanConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace MonBureau.UI.Converters
{
    public class LessThanConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] is int current && values[1] is int total)
            {
                return current < total;
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}