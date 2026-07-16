using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PMTALL.Helpers;

/// <summary>
/// Helper methods for generating file names and sorting prefixes.
/// </summary>
public static class FileNameHelper
{
    /// <summary>
    /// Generate an alphabetic prefix for sorting: a, b, ..., z, aa, ab, ...
    /// </summary>
    public static string GetAlphaPrefix(int index, int totalCount)
    {
        // Use numeric zero-padded if more than 702 (26*27) files
        if (totalCount > 702)
            return (index + 1).ToString("D4") + ".";

        // Use alphabetic prefix
        int num = index;
        string prefix = "";
        do
        {
            prefix = (char)('a' + (num % 26)) + prefix;
            num = num / 26 - 1;
        } while (num >= 0);

        return prefix + ".";
    }

    /// <summary>
    /// Sanitize a string for use as a folder/file name.
    /// </summary>
    public static string SanitizeForPath(string? input)
    {
        if (string.IsNullOrEmpty(input)) return "Unknown";
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            sb.Append(invalid.Contains(ch) ? '_' : ch);
        }
        var result = sb.ToString().Trim();
        return string.IsNullOrEmpty(result) ? "Unknown" : result;
    }

    /// <summary>
    /// Format a DICOM date (YYYYMMDD) to YYMMDD display format.
    /// </summary>
    public static string FormatDateShort(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr) || dateStr.Length < 8)
            return "000000";
        return dateStr[2..8]; // YYMMDD
    }

    /// <summary>
    /// Format a DICOM date to YYYYMMDD.
    /// </summary>
    public static string FormatDateLong(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr) || dateStr.Length < 8)
            return "00000000";
        return dateStr[..8];
    }

    /// <summary>
    /// Get year from DICOM date string.
    /// </summary>
    public static string GetYear(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr) || dateStr.Length < 4)
            return "0000";
        return dateStr[..4];
    }

    /// <summary>
    /// Get month from DICOM date string.
    /// </summary>
    public static string GetMonth(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr) || dateStr.Length < 6)
            return "00";
        return dateStr[4..6];
    }
}
