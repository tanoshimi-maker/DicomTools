namespace PMTALL.Models;

/// <summary>
/// A complete 3D volume reconstructed from a DICOM series (CT or MR).
/// Slices are sorted by Z position (inferior→superior).
/// </summary>
public class CtVolume
{
    public string SeriesInstanceUid { get; set; } = string.Empty;
    public string StudyInstanceUid { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string Modality { get; set; } = string.Empty;
    public string SeriesDescription { get; set; } = string.Empty;

    /// <summary>Slices sorted by ZPosition ascending (inferior→superior).</summary>
    public List<CtSlice> Slices { get; set; } = new();

    // --- Computed properties ---
    public int Depth => Slices.Count;
    public int Rows => Slices.Count > 0 ? Slices[0].Rows : 0;
    public int Columns => Slices.Count > 0 ? Slices[0].Columns : 0;

    /// <summary>
    /// Whether this volume contains enough data for MPR viewing.
    /// </summary>
    public bool IsViewable => Depth >= 1 && Rows >= 1 && Columns >= 1;
}
