using DicomClassifier.Models;

namespace DicomClassifier.Services;

/// <summary>
/// Service for converting raw binary pixel data to DICOM CT Image Storage files.
/// </summary>
public interface IRawToDicomService
{
    /// <summary>
    /// Read a single slice's pixel data from a raw binary file for preview.
    /// </summary>
    /// <param name="rawFilePath">Path to the raw binary file.</param>
    /// <param name="sliceIndex">0-based slice index to read.</param>
    /// <param name="config">Conversion configuration with dimension and offset settings.</param>
    /// <returns>Pixel values as signed 16-bit integers.</returns>
    short[] ReadSlice(string rawFilePath, int sliceIndex, RawConversionConfig config);

    /// <summary>
    /// Convert the entire 3D raw volume into a sequence of DICOM CT files (one per slice).
    /// </summary>
    /// <param name="config">Conversion configuration.</param>
    /// <param name="outputDirectory">Directory to write the DICOM files into.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <returns>List of output file paths.</returns>
    Task<List<string>> ConvertAsync(
        RawConversionConfig config,
        string outputDirectory,
        IProgress<ProgressReport>? progress = null);
}
