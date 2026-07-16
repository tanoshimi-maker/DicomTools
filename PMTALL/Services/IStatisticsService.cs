using PMTALL.Helpers;
using PMTALL.Models;
using FellowOakDicom;

namespace PMTALL.Services;

public interface IStatisticsService
{
    Task<StatisticsReport> ComputeStatisticsAsync(
        List<DicomFileInfo> files,
        IProgress<ProgressReport>? progress = null);
}
