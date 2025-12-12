using System;
using System.Globalization;
using System.Windows.Data;
using MonBureau.Core.Enums;

namespace MonBureau.UI.Converters
{
    public class ItemTypeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ItemType type)
            {
                return type switch
                {
                    ItemType.Document => "📄 Document",
                    ItemType.Hearing => "⚖️ Audience",
                    ItemType.Expense => "💰 Dépense",
                    ItemType.Note => "📝 Note",
                    ItemType.Task => "✓ Tâche",
                    _ => type.ToString()
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