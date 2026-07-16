using PMTALL.Models;

namespace PMTALL.Services;

/// <summary>
/// Service for scanning and parsing DICOM files.
/// </summary>
public interface IDicomScanService
{
    /// <summary>
    /// Scan a directory (non-recursive) for DICOM files.
    /// </summary>
    Task<List<DicomFileInfo>> ScanDirectoryAsync(string directoryPath, IProgress<ProgressReport>? progress = null);

    /// <summary>
    /// Scan a directory tree recursively for DICOM files.
    /// </summary>
    Task<List<DicomFileInfo>> ScanTreeAsync(string rootPath, IProgress<ProgressReport>? progress = null);
}
