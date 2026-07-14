namespace DicomClassifier.Models;

/// <summary>
/// Configuration for converting raw binary pixel data to DICOM CT images.
/// All fields have sensible defaults for a 512³ 16-bit signed CT volume.
/// </summary>
public class RawConversionConfig
{
    // === Raw File Parameters ===
    public string RawFilePath { get; set; } = string.Empty;  // Path to the raw binary file
    public int Width { get; set; } = 512;               // Columns (0028,0011)
    public int Height { get; set; } = 512;              // Rows (0028,0010)
    public int Depth { get; set; } = 512;               // Total slice count
    public int SliceStart { get; set; } = 0;            // First slice to convert (0-based, inclusive)
    public int SliceEnd { get; set; } = 511;            // Last slice to convert (0-based, inclusive)
    public int SliceCount => SliceEnd - SliceStart + 1; // Number of slices to convert
    public int BitsAllocated { get; set; } = 16;        // (0028,0100)
    public int BitsStored { get; set; } = 16;           // (0028,0101)
    public int HighBit { get; set; } = 15;              // (0028,0102)
    public int PixelRepresentation { get; set; } = 1;   // 0=unsigned, 1=signed (2's complement)
    public bool IsLittleEndian { get; set; } = true;
    public int HeaderOffset { get; set; } = 0;          // Bytes to skip at file start

    // === Patient Info ===
    public string PatientId { get; set; } = "TEST001";
    public string PatientName { get; set; } = "Test^Phantom";
    public string PatientSex { get; set; } = "O";
    public string PatientBirthDate { get; set; } = "";

    // === Study Info ===
    public string StudyDate { get; set; } = DateTime.Now.ToString("yyyyMMdd");
    public string StudyId { get; set; } = "1";
    public string StudyDescription { get; set; } = "Raw Conversion Study";

    // === Spatial Info ===
    public double PixelSpacingRow { get; set; } = 0.5;
    public double PixelSpacingCol { get; set; } = 0.5;
    public double SliceThickness { get; set; } = 1.0;
    public double SliceLocationStart { get; set; } = 0.0;

    // Image Orientation (Patient) — 6 values (row direction + column direction)
    public double IopR0 { get; set; } = 1.0;
    public double IopR1 { get; set; } = 0.0;
    public double IopR2 { get; set; } = 0.0;
    public double IopC0 { get; set; } = 0.0;
    public double IopC1 { get; set; } = 1.0;
    public double IopC2 { get; set; } = 0.0;

    // === CT Value Conversion ===
    public double RescaleIntercept { get; set; } = -1024.0;
    public double RescaleSlope { get; set; } = 1.0;

    // === Window / Level (for preview rendering) ===
    public double WindowCenter { get; set; } = 40.0;
    public double WindowWidth { get; set; } = 400.0;

    // === Equipment ===
    public double Kvp { get; set; } = 120.0;

    // === Derived Properties ===
    public int BytesPerPixel => BitsAllocated / 8;
    public int SliceSizeBytes => Width * Height * BytesPerPixel;
    public long TotalDataBytes => (long)Depth * SliceSizeBytes;

    /// <summary>
    /// Byte offset to the start of the first slice to convert.
    /// </summary>
    public long SliceStartOffset => HeaderOffset + (long)SliceStart * SliceSizeBytes;

    /// <summary>
    /// Total bytes needed to read from SliceStart through SliceEnd.
    /// </summary>
    public long SelectedDataBytes => (long)(SliceEnd + 1) * SliceSizeBytes;

    /// <summary>
    /// Verify that the raw file size matches the configured dimensions.
    /// </summary>
    public bool ValidateFileSize(long actualFileSize)
    {
        return actualFileSize >= HeaderOffset + TotalDataBytes;
    }

    /// <summary>
    /// Image Orientation (Patient) as a formatted DICOM string.
    /// </summary>
    public string ImageOrientationPatient =>
        $"{IopR0}\\{IopR1}\\{IopR2}\\{IopC0}\\{IopC1}\\{IopC2}";
}
