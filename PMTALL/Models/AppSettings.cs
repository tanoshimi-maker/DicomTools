namespace PMTALL.Models;

public class AppSettings
{
    public string? SourceFolder { get; set; }
    public string? SortTargetFolder { get; set; }
    public string? ClassifyTargetFolder { get; set; }
    public bool IsSortMode { get; set; } = true;
    public bool SortInPlace { get; set; } = false;
    public bool UseYearMonth { get; set; } = true;
    public int SelectedNamingRuleIndex { get; set; }
    public bool EnableSortWithinPatient { get; set; }
    public bool UsePatientDateModality { get; set; }
    public bool UseSpecialDateMode { get; set; }
    public int SelectedSpecialDateIndex { get; set; }
    public string? BaseTimeString { get; set; }
    public int SelectedTabIndex { get; set; }
    // DoseFix settings
    public string? DoseFixTargetFolder { get; set; }
    public bool DoseFixEnhanceMode { get; set; } = true;

    // RawToDicom settings
    public string? RawSourceFilePath { get; set; }
    public string? RawOutputFolder { get; set; }
    public string? RawPatientId { get; set; }
    public string? RawPatientName { get; set; }
    public string? RawPatientSex { get; set; }
    public string? RawStudyDescription { get; set; }
    public string? RawPixelSpacing { get; set; }
    public string? RawSliceThickness { get; set; }
    public string? RawRescaleIntercept { get; set; }
    public string? RawRescaleSlope { get; set; }
    public string? RawWindowCenter { get; set; }
    public string? RawWindowWidth { get; set; }
    public string? RawKvp { get; set; }
    public string? RawSliceStart { get; set; }
    public string? RawSliceEnd { get; set; }

    // Viewer settings
    public string? ViewerSourceFolder { get; set; }
    public int ViewerPresetIndex { get; set; }
    public string? ViewerSlotA_WindowCenter { get; set; }
    public string? ViewerSlotA_WindowWidth { get; set; }
    public string? ViewerSlotB_WindowCenter { get; set; }
    public string? ViewerSlotB_WindowWidth { get; set; }
}
