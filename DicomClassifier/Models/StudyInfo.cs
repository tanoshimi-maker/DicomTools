namespace DicomClassifier.Models;

/// <summary>
/// Study grouping information.
/// </summary>
public class StudyInfo
{
    public string? StudyInstanceUid { get; set; }
    public string? StudyDate { get; set; }
    public string? StudyTime { get; set; }

    /// <summary>
    /// Series belonging to this study, keyed by SeriesInstanceUid.
    /// </summary>
    public Dictionary<string, SeriesInfo> Series { get; set; } = new();

    /// <summary>
    /// Files that could not be assigned to any series.
    /// </summary>
    public List<DicomFileInfo> OrphanFiles { get; set; } = new();
}
