using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DicomClassifier.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool boolVal = value is true;
        bool invert = parameter?.ToString() == "invert";
        if (invert) boolVal = !boolVal;
        return boolVal ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Visibility.Visible;
    }
}
