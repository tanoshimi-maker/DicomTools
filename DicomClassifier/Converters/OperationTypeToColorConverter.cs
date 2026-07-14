using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using DicomClassifier.Models;

namespace DicomClassifier.Converters;

public class OperationTypeToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FileOperation op)
        {
            return op.Type switch
            {
                OperationType.Copy => new SolidColorBrush(Color.FromRgb(0, 180, 216)),
                OperationType.Move => new SolidColorBrush(Color.FromRgb(76, 201, 240)),
                OperationType.Rename => new SolidColorBrush(Color.FromRgb(114, 9, 183)),
                OperationType.ChangeTime => new SolidColorBrush(Color.FromRgb(247, 127, 0)),
                OperationType.CreateDirectory => new SolidColorBrush(Color.FromRgb(80, 200, 120)),
                _ => new SolidColorBrush(Color.FromRgb(200, 200, 200))
            };
        }
        return new SolidColorBrush(Color.FromRgb(200, 200, 200));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
