using System;
using System.Globalization;
using System.Windows.Data;
using MonBureau.Core.Enums;

namespace MonBureau.UI.Converters
{
    public class CaseStatusToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CaseStatus status)
            {
                return status switch
                {
                    CaseStatus.Open => "Ouvert",
                    CaseStatus.InProgress => "En Cours",
                    CaseStatus.Closed => "Fermé",
                    CaseStatus.Archived => "Archivé",
                    CaseStatus.OnHold => "En Attente",
                    _ => status.ToString()
                };
            }

            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
