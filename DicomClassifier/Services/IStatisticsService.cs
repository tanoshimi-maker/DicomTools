using DicomClassifier.Helpers;
using DicomClassifier.Models;
using FellowOakDicom;

namespace DicomClassifier.Services;

public interface IStatisticsService
{
    Task<StatisticsReport> ComputeStatisticsAsync(
        List<DicomFileInfo> files,
        IProgress<ProgressReport>? progress = null);
}
