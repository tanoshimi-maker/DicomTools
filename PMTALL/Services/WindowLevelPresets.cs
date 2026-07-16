namespace PMTALL.Services;

/// <summary>
/// A window/level preset for medical image viewing.
/// </summary>
public record WindowLevelPreset(string Name, double Center, double Width, string Modality);

/// <summary>
/// Built-in window/level presets for CT and MR modalities.
/// </summary>
public static class WindowLevelPresets
{
    public static List<WindowLevelPreset> All { get; } = new()
    {
        // --- CT Presets ---
        new("Abdomen Soft Tissue", 40, 400, "CT"),
        new("Liver", 70, 150, "CT"),
        new("Lung", -500, 1500, "CT"),
        new("Bone", 400, 1800, "CT"),
        new("Brain", 35, 80, "CT"),
        new("Mediastinum", 50, 500, "CT"),
        new("C-Spine", 300, 1800, "CT"),

        // --- MR Presets ---
        new("MR T1", 300, 600, "MR"),
        new("MR T2", 1000, 2000, "MR"),
        new("MR FLAIR", 500, 1500, "MR"),
        new("MR DWI", 600, 1200, "MR"),

        // --- Manual ---
        new("Manual", 40, 400, "All"),
    };
}
