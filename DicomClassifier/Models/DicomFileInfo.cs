using FellowOakDicom;

namespace DicomClassifier.Models;

/// <summary>
/// Represents a single DICOM file with parsed metadata tags.
/// </summary>
public class DicomFileInfo
{
    public string SourcePath { get; set; } = string.Empty;
    public string FileName => System.IO.Path.GetFileName(SourcePath);

    // Patient
    public string? PatientId { get; set; }
    public string? PatientName { get; set; }

    // Study
    public string? StudyInstanceUid { get; set; }
    public string? StudyDate { get; set; }
    public string? StudyTime { get; set; }

    // Series
    public string? SeriesInstanceUid { get; set; }
    public string? SopInstanceUid { get; set; }
    public string? SeriesDate { get; set; }
    public string? SeriesTime { get; set; }
    public string? Modality { get; set; }

    // Acquisition
    public string? AcquisitionDate { get; set; }
    public string? AcquisitionTime { get; set; }

    // Content
    public string? ContentDate { get; set; }
    public string? ContentTime { get; set; }

    // Frame of Reference
    public string? FrameOfReferenceUid { get; set; }

    // Manufacturer
    public string? ManufacturerModelName { get; set; }

    // RT references
    public string? ReferencedSopInstanceUid { get; set; }

    /// <summary>
    /// Best available acquisition datetime for sorting.
    /// Priority: AcquisitionDateTime > ContentDateTime > SeriesDateTime > StudyDateTime
    /// </summary>
    public DateTime? AcquisitionTimestamp { get; set; }

    /// <summary>
    /// Whether this file was successfully parsed as DICOM.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Error message if parsing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Original file last write time.
    /// </summary>
    public DateTime OriginalLastWriteTime { get; set; }

    /// <summary>
    /// The underlying DicomDataset for advanced access.
    /// Only populated during scanning; not serialized.
    /// </summary>
    public DicomDataset? Dataset { get; set; }
}
