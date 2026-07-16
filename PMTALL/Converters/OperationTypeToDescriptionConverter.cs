using System.Globalization;
using System.Windows.Data;
using PMTALL.Models;

namespace PMTALL.Converters;

public class OperationTypeToDescriptionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FileOperation op)
        {
            return op.Type switch
            {
                OperationType.Copy => $"Copy to: {TruncatePath(op.DestinationPath)}",
                OperationType.Move => $"Move to: {TruncatePath(op.DestinationPath)}",
                OperationType.Rename => $"Rename: {op.OldName} → {op.NewName}",
                OperationType.ChangeTime => $"Set time: {op.NewLastWriteTime:yyyy-MM-dd HH:mm:ss}",
                OperationType.CreateDirectory => $"Create: {TruncatePath(op.DestinationPath)}",
                _ => op.Description
            };
        }
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static string TruncatePath(string path, int maxLen = 80)
    {
        return path.Length > maxLen ? "..." + path[^(maxLen - 3)..] : path;
    }
}
