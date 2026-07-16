using PMTALL.Models;

namespace PMTALL.Services;

public interface IExportService
{
    Task ExportToExcelAsync(StatisticsReport report, string filePath);
    Task ExportToJsonAsync(StatisticsReport report, string filePath);
    Task ExportToCsvAsync(StatisticsReport report, string filePath);
}
