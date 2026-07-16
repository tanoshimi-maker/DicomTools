using System.Text.RegularExpressions;

namespace PMTALL.Models;

/// <summary>
/// Series grouping information with RT binding support.
/// </summary>
public class SeriesInfo
{
    public string? SeriesInstanceUid { get; set; }
    public string? SeriesDate { get; set; }
    public string? SeriesTime { get; set; }
    public string? Modality { get; set; }
    public string? FrameOfReferenceUid { get; set; }

    /// <summary>
    /// Files in this series.
    /// </summary>
    public List<DicomFileInfo> Files { get; set; } = new();

    /// <summary>
    /// Referenced SOP Instance UIDs (for RT objects linking to CT).
    /// </summary>
    public List<string> ReferencedSopInstanceUids { get; set; } = new();

    /// <summary>
    /// Manufacturer's Model Name from the first file in the series.
    /// </summary>
    public string? ManufacturerModelName => Files.FirstOrDefault()?.ManufacturerModelName;

    /// <summary>
    /// Whether this series is a CBCT (modality is CT but manufacturer indicates CBCT).
    /// </summary>
    public bool IsCbct =>
        Modality == "CT" &&
        ManufacturerModelName != null &&
        (ManufacturerModelName.Contains("CBCT", StringComparison.OrdinalIgnoreCase) ||
         ManufacturerModelName.Contains("cone", StringComparison.OrdinalIgnoreCase) ||
         Regex.IsMatch(ManufacturerModelName, @"\bCB\b", RegexOptions.IgnoreCase));

    /// <summary>
    /// Whether this is a true CT series (not CBCT).
    /// </summary>
    public bool IsCtSeries => Modality == "CT" && !IsCbct;

    /// <summary>
    /// Whether this is an RT object (plan, dose, structure, registration).
    /// </summary>
    public bool IsRtObject => Modality is "RTPLAN" or "RTDOSE" or "RTSTRUCT" or "REG";
}
