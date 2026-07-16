namespace PMTALL.Models;

/// <summary>
/// Represents a single slice in a CT/MR volumetric series.
/// </summary>
public class CtSlice
{
    public string SourcePath { get; set; } = string.Empty;
    public string SopInstanceUid { get; set; } = string.Empty;
    public int InstanceNumber { get; set; }

    // --- Spatial tags ---
    /// <summary>ImagePositionPatient (0020,0032): x, y, z of top-left pixel</summary>
    public double[] ImagePositionPatient { get; set; } = new double[3];

    /// <summary>ImageOrientationPatient (0020,0037): rowX, rowY, rowZ, colX, colY, colZ</summary>
    public double[] ImageOrientationPatient { get; set; } = new double[6];

    /// <summary>PixelSpacing (0028,0030): row spacing, column spacing</summary>
    public double[] PixelSpacing { get; set; } = new double[2];

    /// <summary>SliceThickness (0018,0050)</summary>
    public double SliceThickness { get; set; }

    // --- Dimensions ---
    public int Rows { get; set; }
    public int Columns { get; set; }

    // --- Pixel data ---
    public short[] Pixels { get; set; } = Array.Empty<short>();

    // --- CT value calibration ---
    public double RescaleIntercept { get; set; }
    public double RescaleSlope { get; set; } = 1.0;

    // --- DICOM window/level defaults for this slice ---
    public double WindowCenter { get; set; } = 40;
    public double WindowWidth { get; set; } = 400;

    /// <summary>
    /// Z position along the slice normal, used for sorting slices inferior→superior.
    /// For standard axial acquisitions (IOP ≈ [1,0,0, 0,1,0]), this is simply IPP[2].
    /// </summary>
    public double ZPosition { get; set; }
}
