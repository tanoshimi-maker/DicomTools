namespace PMTALL.Models;

public class StatisticsReport
{
    // Basic counts
    public int TotalPatients { get; set; }
    public int TotalStudies { get; set; }
    public int TotalSeries { get; set; }
    public int TotalFiles { get; set; }
    public int ValidFiles { get; set; }
    public int InvalidFiles { get; set; }

    // Gender distribution
    public int MaleCount { get; set; }
    public int FemaleCount { get; set; }
    public int GenderUnknownCount { get; set; }

    // Age
    public double? AverageAge { get; set; }
    public int AgeComputedCount { get; set; }

    // Modality distribution
    public Dictionary<string, int> ModalityCounts { get; set; } = new();

    // Time span
    public string? EarliestStudyDate { get; set; }
    public string? LatestStudyDate { get; set; }

    // Patient metrics
    public double AverageStudiesPerPatient { get; set; }
    public double AverageInstancesPerPatient { get; set; }

    // Missing tags
    public int MissingKeyTagsCount { get; set; }

    // RTPLAN
    public int TotalRtPlanCount { get; set; }
    public int TotalRtDoseCount { get; set; }
    public int TotalRtStructCount { get; set; }

    // Total / dose file sizes
    public long TotalFolderSizeBytes { get; set; }
    public long DoseFilesSizeBytes { get; set; }

    // Manufacturer distribution
    public Dictionary<string, int> ManufacturerCounts { get; set; } = new();

    // Anonymization
    public int FilesWithPatientName { get; set; }
    public double AnonymizationPercent { get; set; }
}
