using System.Globalization;
using System.Windows;
using System.Windows.Data;
using PMTALL.Models;

namespace PMTALL.Converters;

/// <summary>
/// Maps a DicomTreeNode's NodeType to a Unicode icon character for TreeView display.
/// </summary>
public class TreeViewIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string nodeType) return "📄";

        return nodeType switch
        {
            "Folder" => "📁",
            "Patient" => "👤",
            "Study" => "📋",
            "Series" => "🔬",
            "File" => "📄",
            _ => "📄"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Converts a DicomTreeNode's NodeType to a Visibility value.
/// Only Series nodes are shown as viewable (clickable to load).
/// </summary>
public class NodeTypeToLoadButtonVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string nodeType) return Visibility.Collapsed;
        return nodeType == "Series" ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
