using PMTALL.Models;

namespace PMTALL.Services;

/// <summary>
/// Service for fixing RTDOSE file spatial alignment by correcting
/// Image Orientation (Patient) and Image Position (Patient) tags.
/// </summary>
public interface IDoseFixService
{
    /// <summary>
    /// Build an operation plan to copy all files to the target directory,
    /// applying dose correction to RTDOSE files.
    /// Non-RTDOSE files are copied as-is.
    /// Directory structure is preserved.
    /// </summary>
    OperationPlan BuildDoseFixPlan(
        List<DicomFileInfo> files,
        string targetRoot,
        bool enhanceMode);

    /// <summary>
    /// Fix a single RTDOSE file: read the original, apply IOP/IPP correction,
    /// and save the fixed version to the destination path.
    /// </summary>
    void FixDoseFile(
        string sourcePath,
        string destinationPath,
        double[] newIop,
        double[] newIpp);
}
