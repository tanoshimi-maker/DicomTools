using DicomClassifier.Models;

namespace DicomClassifier.Services;

/// <summary>
/// Service for planning sort-mode operations on DICOM files.
/// </summary>
public interface ISortService
{
    /// <summary>
    /// Build an operation plan to sort DICOM files by acquisition time.
    /// </summary>
    OperationPlan BuildSortPlan(
        List<DicomFileInfo> files,
        string? targetDirectory = null,
        DateTime? baseTime = null);
}
