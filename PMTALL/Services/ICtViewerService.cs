using System.Collections.ObjectModel;
using PMTALL.Models;

namespace PMTALL.Services;

/// <summary>
/// Service for scanning DICOM folders, building tree hierarchies, and loading CT/MR volumes.
/// </summary>
public interface ICtViewerService
{
    /// <summary>
    /// Scan a folder (recursively) for DICOM files and return a flat list.
    /// </summary>
    Task<List<DicomFileInfo>> ScanFolderAsync(string path, IProgress<ProgressReport>? progress = null);

    /// <summary>
    /// Build a folder-hierarchy tree from scanned DICOM files.
    /// </summary>
    ObservableCollection<DicomTreeNode> BuildFolderTree(List<DicomFileInfo> files);

    /// <summary>
    /// Build a DICOM tag hierarchy: Patient → Study → Series → Files.
    /// </summary>
    ObservableCollection<DicomTreeNode> BuildDicomHierarchyTree(List<DicomFileInfo> files);

    /// <summary>
    /// Load all slices of a series into a CtVolume, sorted by Z position.
    /// </summary>
    Task<CtVolume> LoadVolumeAsync(
        string seriesInstanceUid,
        List<DicomFileInfo> seriesFiles,
        IProgress<ProgressReport>? progress = null);
}
