using System.Globalization;
using System.Windows.Data;

namespace KnowledgeAssistant.Wpf.Converters;

public class FavoriteModelDisplayConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not string name)
        {
            return values.Length > 0 ? values[0]?.ToString() ?? string.Empty : string.Empty;
        }

        var isFavorite = values[1] is HashSet<string> favorites && favorites.Contains(name);
        return isFavorite ? $"\u2605 {name}" : name;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}