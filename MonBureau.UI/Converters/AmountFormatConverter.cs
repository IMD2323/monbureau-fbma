using System;
using System.Globalization;
using System.Windows.Data;

namespace MonBureau.UI.Converters
{
    public class AmountFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal amount)
            {
                return $"{amount:N2} DA";
            }

            if (value is double doubleAmount)
            {
                return $"{doubleAmount:N2} DA";
            }

            return "0.00 DA";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string strValue)
            {
                var cleaned = strValue.Replace("DA", "").Replace(" ", "").Trim();
                if (decimal.TryParse(cleaned, out var result))
                {
                    return result;
                }
            }

            return 0m;
        }
    }
}