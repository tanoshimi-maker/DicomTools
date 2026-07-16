using PMTALL.Helpers;
using PMTALL.Models;
using FellowOakDicom;

namespace PMTALL.Services;

public class StatisticsService : IStatisticsService
{
    public async Task<StatisticsReport> ComputeStatisticsAsync(
        List<DicomFileInfo> files,
        IProgress<ProgressReport>? progress = null)
    {
        var report = new StatisticsReport();
        var validFiles = files.Where(f => f.IsValid).ToList();
        var invalidFiles = files.Where(f => !f.IsValid).ToList();

        report.TotalFiles = files.Count;
        report.ValidFiles = validFiles.Count;
        report.InvalidFiles = invalidFiles.Count;

        progress?.Report(new ProgressReport { Current = 0, Total = 10, Status = "Grouping patients..." });

        // Group by PatientId
        var patientGroups = validFiles
            .GroupBy(f => f.PatientId ?? "__NOPID__")
            .ToList();

        report.TotalPatients = patientGroups.Count;

        // Compute total folder size
        progress?.Report(new ProgressReport { Current = 1, Total = 10, Status = "Computing folder size..." });
        report.TotalFolderSizeBytes = validFiles.Sum(f =>
        {
            try { return new FileInfo(f.SourcePath).Length; }
            catch { return 0L; }
        });

        // Gender distribution
        progress?.Report(new ProgressReport { Current = 2, Total = 10, Status = "Computing gender distribution..." });
        foreach (var grp in patientGroups)
        {
            var firstFile = grp.First();
            if (firstFile.Dataset != null)
            {
                var sex = DicomTagHelper.GetString(firstFile.Dataset, DicomTag.PatientSex) ?? "";
                sex = sex.Trim().ToUpperInvariant();
                if (sex == "M") report.MaleCount++;
                else if (sex == "F") report.FemaleCount++;
                else report.GenderUnknownCount++;
            }
            else
            {
                report.GenderUnknownCount++;
            }
        }

        // Age distribution (compute once per patient using earliest study)
        progress?.Report(new ProgressReport { Current = 3, Total = 10, Status = "Computing age distribution..." });
        int ageSum = 0;
        int ageCount = 0;
        foreach (var grp in patientGroups)
        {
            var firstFile = grp.First();
            if (firstFile.Dataset == null) continue;

            var birthDate = DicomTagHelper.ParseDate(DicomTagHelper.GetString(firstFile.Dataset, DicomTag.PatientBirthDate));
            if (birthDate == null) continue;

            // Find earliest study date for this patient
            var earliestStudy = grp
                .Select(f => DicomTagHelper.ParseDate(f.StudyDate))
                .Where(d => d.HasValue)
                .OrderBy(d => d!.Value)
                .FirstOrDefault();

            if (earliestStudy.HasValue)
            {
                var ageAtStudy = earliestStudy.Value.Year - birthDate.Value.Year;
                // Adjust if birthday hasn't occurred yet in the study year
                if (earliestStudy.Value < birthDate.Value.AddYears(ageAtStudy))
                    ageAtStudy--;
                if (ageAtStudy >= 0 && ageAtStudy <= 120)
                {
                    ageSum += ageAtStudy;
                    ageCount++;
                }
            }
        }
        if (ageCount > 0)
        {
            report.AverageAge = Math.Round((double)ageSum / ageCount, 1);
            report.AgeComputedCount = ageCount;
        }

        // RTPLAN / RTDOSE / RTSTRUCT counts & dose file size
        progress?.Report(new ProgressReport { Current = 4, Total = 10, Status = "Counting RT instances..." });
        report.TotalRtPlanCount = validFiles.Count(f => f.Modality == "RTPLAN");
        report.TotalRtDoseCount = validFiles.Count(f => f.Modality == "RTDOSE");
        report.TotalRtStructCount = validFiles.Count(f => f.Modality == "RTSTRUCT");

        report.DoseFilesSizeBytes = validFiles
            .Where(f => f.Modality == "RTDOSE")
            .Sum(f =>
            {
                try { return new FileInfo(f.SourcePath).Length; }
                catch { return 0L; }
            });

        // Modality distribution
        progress?.Report(new ProgressReport { Current = 5, Total = 10, Status = "Computing modality distribution..." });
        report.ModalityCounts = validFiles
            .GroupBy(f => f.Modality ?? "UNKNOWN")
            .ToDictionary(g => g.Key, g => g.Count());

        // Study counts (by StudyInstanceUid)
        progress?.Report(new ProgressReport { Current = 6, Total = 10, Status = "Counting studies..." });
        var studyUids = validFiles
            .Select(f => f.StudyInstanceUid)
            .Where(u => u != null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        report.TotalStudies = studyUids;

        report.AverageStudiesPerPatient = report.TotalPatients > 0
            ? Math.Round((double)studyUids / report.TotalPatients, 1)
            : 0;

        // Average instances per patient
        report.AverageInstancesPerPatient = report.TotalPatients > 0
            ? Math.Round((double)validFiles.Count / report.TotalPatients, 1)
            : 0;

        // Time span
        progress?.Report(new ProgressReport { Current = 7, Total = 10, Status = "Computing time span..." });
        var dates = validFiles
            .Select(f => f.StudyDate)
            .Where(d => d != null && d.Length >= 8)
            .ToList();
        if (dates.Count > 0)
        {
            dates.Sort();
            report.EarliestStudyDate = dates.First();
            report.LatestStudyDate = dates.Last();
        }

        // Missing key tags
        progress?.Report(new ProgressReport { Current = 8, Total = 10, Status = "Checking missing tags..." });
        report.MissingKeyTagsCount = validFiles.Count(f =>
            string.IsNullOrEmpty(f.PatientId) ||
            string.IsNullOrEmpty(f.StudyDate) ||
            string.IsNullOrEmpty(f.Modality));

        // Manufacturer distribution
        progress?.Report(new ProgressReport { Current = 9, Total = 10, Status = "Computing manufacturer distribution..." });
        report.ManufacturerCounts = validFiles
            .Select(f => f.Dataset != null ? DicomTagHelper.GetString(f.Dataset, DicomTag.Manufacturer) ?? "Unknown" : "Unknown")
            .GroupBy(m => m)
            .ToDictionary(g => g.Key, g => g.Count());

        // Anonymization
        report.FilesWithPatientName = validFiles.Count(f => !string.IsNullOrEmpty(f.PatientName));
        report.AnonymizationPercent = report.ValidFiles > 0
            ? Math.Round((double)(report.ValidFiles - report.FilesWithPatientName) / report.ValidFiles * 100, 1)
            : 0;

        // Series count
        report.TotalSeries = validFiles
            .Select(f => f.SeriesInstanceUid)
            .Where(u => u != null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        progress?.Report(new ProgressReport { Current = 10, Total = 10, Status = "Statistics complete." });

        return report;
    }
}
