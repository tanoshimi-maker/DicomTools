using FellowOakDicom;

namespace PMTALL.Models;

/// <summary>
/// Configuration for Special Date Mode: use RT file date tags as classification base.
/// </summary>
public class SpecialDateConfig
{
    public string DisplayName { get; set; } = string.Empty;
    public DicomTag Tag { get; set; } = DicomTag.Unknown;
    public string[] ApplicableModalities { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Built-in RT date tag options the user can select from as classification date base.
/// </summary>
public static class SpecialDateOptions
{
    public static readonly List<SpecialDateConfig> Options = new()
    {
        new()
        {
            DisplayName = "RTSTRUCT: Structure Set Date (3006,0008)",
            Tag = new DicomTag(0x3006, 0x0008),
            ApplicableModalities = ["RTSTRUCT"]
        },
        new()
        {
            DisplayName = "RTPLAN: RT Plan Date (300A,0003)",
            Tag = new DicomTag(0x300A, 0x0003),
            ApplicableModalities = ["RTPLAN"]
        },
        new()
        {
            DisplayName = "RTPLAN: Review Date (300E,0004)",
            Tag = new DicomTag(0x300E, 0x0004),
            ApplicableModalities = ["RTPLAN"]
        },
        new()
        {
            DisplayName = "RTDOSE: Dose Date (3004,000A)",
            Tag = new DicomTag(0x3004, 0x000A),
            ApplicableModalities = ["RTDOSE"]
        },
        new()
        {
            DisplayName = "REG: Registration Date (0070,0082)",
            Tag = new DicomTag(0x0070, 0x0082),
            ApplicableModalities = ["REG"]
        },
    };
}
