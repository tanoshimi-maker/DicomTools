namespace DicomClassifier.Models;

/// <summary>
/// Patient grouping information extracted from DICOM files.
/// </summary>
public class PatientInfo
{
    public string? PatientId { get; set; }
    public string? PatientName { get; set; }

    /// <summary>
    /// Earliest study date across all patient's studies, used for ordering.
    /// </summary>
    public string? EarliestStudyDate { get; set; }

    /// <summary>
    /// Studies belonging to this patient, keyed by StudyInstanceUid.
    /// </summary>
    public Dictionary<string, StudyInfo> Studies { get; set; } = new();

    /// <summary>
    /// Files that could not be assigned to any study.
    /// </summary>
    public List<DicomFileInfo> OrphanFiles { get; set; } = new();

    /// <summary>
    /// Whether this patient has any valid DICOM files.
    /// </summary>
    public bool HasValidData => Studies.Count > 0 || OrphanFiles.Count > 0;
}
