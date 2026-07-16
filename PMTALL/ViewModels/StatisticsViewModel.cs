using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PMTALL.Models;
using PMTALL.Services;
using Microsoft.Win32;

namespace PMTALL.ViewModels;

public partial class StatisticsViewModel : ObservableObject
{
    private readonly IStatisticsService _statisticsService;
    private readonly IExportService _exportService;
    private readonly IDicomScanService _scanService;

    private StatisticsReport? _report;
    private List<DicomFileInfo> _cachedFiles = new();

    public StatisticsViewModel(
        IStatisticsService statisticsService,
        IExportService exportService,
        IDicomScanService scanService)
    {
        _statisticsService = statisticsService;
        _exportService = exportService;
        _scanService = scanService;
        InitStatItems();
    }

    // ===== Source Folder =====
    [ObservableProperty]
    private string _sourceFolder = string.Empty;

    // ===== State =====
    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _hasResults;

    [ObservableProperty]
    private int _progressCurrent;

    [ObservableProperty]
    private int _progressTotal = 100;

    [ObservableProperty]
    private string _progressStatus = "Ready";

    [ObservableProperty]
    private string _scanSummary = string.Empty;

    // ===== Stat Items =====
    public ObservableCollection<StatItem> StatItems { get; } = new();

    // ===== Log =====
    public ObservableCollection<string> LogEntries { get; } = new();

    private void InitStatItems()
    {
        var items = new[]
        {
            ("Total Patients", "Basic"),
            ("Gender Distribution", "Basic"),
            ("Average Age (years)", "Basic"),
            ("Total Studies", "Basic"),
            ("Total Series", "Basic"),
            ("Total RTPLAN Instances", "RT"),
            ("Total RTDOSE Instances", "RT"),
            ("Total RTSTRUCT Instances", "RT"),
            ("Total Folder Size", "Storage"),
            ("Dose Files Size", "Storage"),
            ("Modality Distribution", "Distribution"),
            ("Average Studies/Patient", "Advanced"),
            ("Average Instances/Patient", "Advanced"),
            ("Time Span", "Advanced"),
            ("Missing Key Tags", "Quality"),
            ("Anonymization %", "Quality"),
            ("Manufacturer Distribution", "Distribution"),
        };

        foreach (var (name, category) in items)
        {
            StatItems.Add(new StatItem { Name = name, IsChecked = true, Category = category });
        }
    }

    public bool IsAllChecked
    {
        get => StatItems.All(s => s.IsChecked);
        set
        {
            foreach (var item in StatItems) item.IsChecked = value;
            OnPropertyChanged();
        }
    }

    // ===== Commands =====

    [RelayCommand]
    private void SelectSourceFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Select Source Folder for Statistics" };
        if (dialog.ShowDialog() == true)
        {
            SourceFolder = dialog.FolderName;
        }
    }

    [RelayCommand]
    private async Task StartStatistics()
    {
        if (string.IsNullOrEmpty(SourceFolder) || !System.IO.Directory.Exists(SourceFolder))
        {
            LogEntries.Add("Error: Please select a valid source folder.");
            return;
        }

        IsScanning = true;
        HasResults = false;
        _report = null;
        ScanSummary = string.Empty;

        var progress = new SimpleProgress(p =>
        {
            ProgressCurrent = p.Current;
            ProgressTotal = p.Total;
            ProgressStatus = p.Status;
        });

        try
        {
            LogEntries.Add($"[Scan] Scanning directory tree recursively...");
            var files = await _scanService.ScanTreeAsync(SourceFolder, progress);
            _cachedFiles = files;

            var validCount = files.Count(f => f.IsValid);
            var invalidCount = files.Count(f => !f.IsValid);
            ScanSummary = $"Found {files.Count} files ({validCount} valid, {invalidCount} unreadable).";
            LogEntries.Add($"[Scan] {ScanSummary}");

            LogEntries.Add($"[Stats] Computing statistics...");
            _report = await _statisticsService.ComputeStatisticsAsync(files, progress);

            UpdateStatValues();
            HasResults = true;
            ProgressStatus = "Statistics complete.";

            LogEntries.Add($"[Stats] Computed {StatItems.Count(s => s.IsChecked)} statistics from {_report.TotalPatients} patients.");
        }
        catch (Exception ex)
        {
            LogEntries.Add($"[Error] Statistics failed: {ex.Message}");
            ProgressStatus = "Statistics failed.";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task Export(string format)
    {
        if (_report == null) return;

        var dialog = new SaveFileDialog
        {
            Title = "Export Statistics",
            FileName = $"DicomStats_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        switch (format.ToLowerInvariant())
        {
            case "excel":
                dialog.Filter = "Excel files (*.xlsx)|*.xlsx";
                dialog.DefaultExt = ".xlsx";
                break;
            case "json":
                dialog.Filter = "JSON files (*.json)|*.json";
                dialog.DefaultExt = ".json";
                break;
            case "csv":
                dialog.Filter = "CSV files (*.csv)|*.csv";
                dialog.DefaultExt = ".csv";
                break;
            default:
                return;
        }

        if (dialog.ShowDialog() == true)
        {
            try
            {
                ProgressStatus = $"Exporting to {format.ToUpperInvariant()}...";
                switch (format.ToLowerInvariant())
                {
                    case "excel":
                        await _exportService.ExportToExcelAsync(_report, dialog.FileName);
                        break;
                    case "json":
                        await _exportService.ExportToJsonAsync(_report, dialog.FileName);
                        break;
                    case "csv":
                        await _exportService.ExportToCsvAsync(_report, dialog.FileName);
                        break;
                }
                LogEntries.Add($"[Export] Exported to {dialog.FileName}");
                ProgressStatus = $"Exported to {format.ToUpperInvariant()}";
            }
            catch (Exception ex)
            {
                LogEntries.Add($"[Error] Export failed: {ex.Message}");
                ProgressStatus = "Export failed.";
            }
        }
    }

    [RelayCommand]
    private void ToggleAll()
    {
        IsAllChecked = !IsAllChecked;
    }

    private void UpdateStatValues()
    {
        if (_report == null) return;

        foreach (var item in StatItems)
        {
            item.ComputedValue = item.Name switch
            {
                "Total Patients" => _report.TotalPatients.ToString("N0"),
                "Gender Distribution" => $"M: {_report.MaleCount}  F: {_report.FemaleCount}  U: {_report.GenderUnknownCount}",
                "Average Age (years)" => _report.AverageAge.HasValue
                    ? $"{_report.AverageAge:F1} (n={_report.AgeComputedCount})"
                    : "N/A",
                "Total Studies" => _report.TotalStudies.ToString("N0"),
                "Total Series" => _report.TotalSeries.ToString("N0"),
                "Total RTPLAN Instances" => _report.TotalRtPlanCount.ToString("N0"),
                "Total RTDOSE Instances" => _report.TotalRtDoseCount.ToString("N0"),
                "Total RTSTRUCT Instances" => _report.TotalRtStructCount.ToString("N0"),
                "Total Folder Size" => FormatFileSize(_report.TotalFolderSizeBytes),
                "Dose Files Size" => FormatFileSize(_report.DoseFilesSizeBytes),
                "Modality Distribution" => string.Join(", ", _report.ModalityCounts
                    .OrderByDescending(k => k.Value)
                    .Select(k => $"{k.Key}: {k.Value}")),
                "Average Studies/Patient" => _report.AverageStudiesPerPatient.ToString("F1"),
                "Average Instances/Patient" => _report.AverageInstancesPerPatient.ToString("F1"),
                "Time Span" => $"{_report.EarliestStudyDate ?? "N/A"} ~ {_report.LatestStudyDate ?? "N/A"}",
                "Missing Key Tags" => _report.MissingKeyTagsCount.ToString("N0"),
                "Anonymization %" => $"{_report.AnonymizationPercent}% ({_report.FilesWithPatientName} files have names)",
                "Manufacturer Distribution" => string.Join(", ", _report.ManufacturerCounts
                    .OrderByDescending(k => k.Value)
                    .Select(k => $"{k.Key}: {k.Value}")),
                _ => "-"
            };
        }
    }

    /// <summary>
    /// Handle drag-drop folder path.
    /// </summary>
    public void SetSourceFolderFromDrop(string path)
    {
        if (System.IO.Directory.Exists(path))
        {
            SourceFolder = path;
        }
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogEntries.Clear();
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
