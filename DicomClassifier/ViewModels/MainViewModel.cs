using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DicomClassifier.Models;
using DicomClassifier.Services;
using Microsoft.Win32;

namespace DicomClassifier.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IDicomScanService _scanService;
    private readonly ISortService _sortService;
    private readonly IClassifyService _classifyService;
    private readonly IDoseFixService _doseFixService;
    private readonly IExecutionService _executionService;
    private readonly ISettingsService _settingsService;
    private readonly IRawToDicomService _rawToDicomService;

    public StatisticsViewModel Statistics { get; }

    public MainViewModel(
        IDicomScanService scanService,
        ISortService sortService,
        IClassifyService classifyService,
        IDoseFixService doseFixService,
        IExecutionService executionService,
        ISettingsService settingsService,
        IRawToDicomService rawToDicomService,
        StatisticsViewModel statisticsViewModel)
    {
        _scanService = scanService;
        _sortService = sortService;
        _classifyService = classifyService;
        _doseFixService = doseFixService;
        _executionService = executionService;
        _settingsService = settingsService;
        _rawToDicomService = rawToDicomService;
        Statistics = statisticsViewModel;

        LoadSettings();
    }

    // ===== Tab Navigation =====
    [ObservableProperty]
    private int _selectedTabIndex;

    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsSortMode));
        OnPropertyChanged(nameof(IsClassifyMode));
        OnPropertyChanged(nameof(IsStatisticsMode));
        OnPropertyChanged(nameof(IsDoseFixMode));
        OnPropertyChanged(nameof(IsRawToDicomMode));
        SaveSettings();
    }

    public bool IsSortMode
    {
        get => SelectedTabIndex == 0;
        set { if (value) SelectedTabIndex = 0; }
    }

    public bool IsClassifyMode
    {
        get => SelectedTabIndex == 1;
        set { if (value) SelectedTabIndex = 1; }
    }

    public bool IsStatisticsMode
    {
        get => SelectedTabIndex == 2;
        set { if (value) SelectedTabIndex = 2; }
    }

    public bool IsDoseFixMode
    {
        get => SelectedTabIndex == 3;
        set { if (value) SelectedTabIndex = 3; }
    }

    public bool IsRawToDicomMode
    {
        get => SelectedTabIndex == 4;
        set { if (value) SelectedTabIndex = 4; }
    }

    // ===== Source Folder (shared) =====
    [ObservableProperty]
    private string _sourceFolder = string.Empty;

    // ===== Sort Options =====
    [ObservableProperty]
    private bool _sortInPlace = false;

    [ObservableProperty]
    private string _sortTargetFolder = string.Empty;

    private DateTime _sortBaseTime = new(2000, 1, 1);

    public DateTime SortBaseTime
    {
        get => _sortBaseTime;
        set
        {
            if (SetProperty(ref _sortBaseTime, value))
            {
                OnPropertyChanged(nameof(SortYear));
                OnPropertyChanged(nameof(SortMonth));
                OnPropertyChanged(nameof(SortDay));
                OnPropertyChanged(nameof(SortHour));
                OnPropertyChanged(nameof(SortMinute));
            }
        }
    }

    public int SortYear
    {
        get => _sortBaseTime.Year;
        set
        {
            if (value < 1 || value > 9999) return;
            var dt = _sortBaseTime;
            var maxDay = DateTime.DaysInMonth(value, dt.Month);
            SortBaseTime = new DateTime(value, dt.Month, Math.Min(dt.Day, maxDay), dt.Hour, dt.Minute, 0, dt.Kind);
        }
    }

    public int SortMonth
    {
        get => _sortBaseTime.Month;
        set
        {
            if (value < 1 || value > 12) return;
            var dt = _sortBaseTime;
            var day = Math.Min(dt.Day, DateTime.DaysInMonth(dt.Year, value));
            SortBaseTime = new DateTime(dt.Year, value, day, dt.Hour, dt.Minute, 0, dt.Kind);
        }
    }

    public int SortDay
    {
        get => _sortBaseTime.Day;
        set
        {
            if (value < 1 || value > 31) return;
            var dt = _sortBaseTime;
            var maxDay = DateTime.DaysInMonth(dt.Year, dt.Month);
            SortBaseTime = new DateTime(dt.Year, dt.Month, Math.Min(value, maxDay), dt.Hour, dt.Minute, 0, dt.Kind);
        }
    }

    public int SortHour
    {
        get => _sortBaseTime.Hour;
        set
        {
            if (value < 0 || value > 23) return;
            var dt = _sortBaseTime;
            SortBaseTime = new DateTime(dt.Year, dt.Month, dt.Day, value, dt.Minute, 0, dt.Kind);
        }
    }

    public int SortMinute
    {
        get => _sortBaseTime.Minute;
        set
        {
            if (value < 0 || value > 59) return;
            var dt = _sortBaseTime;
            SortBaseTime = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, value, 0, dt.Kind);
        }
    }

    public List<int> Years { get; } = Enumerable.Range(1990, 111).ToList();
    public List<int> Months { get; } = Enumerable.Range(1, 12).ToList();
    public List<int> Days { get; } = Enumerable.Range(1, 31).ToList();
    public List<int> Hours { get; } = Enumerable.Range(0, 24).ToList();
    public List<int> Minutes { get; } = Enumerable.Range(0, 60).ToList();

    // ===== DoseFix Options =====
    [ObservableProperty]
    private string _doseFixTargetFolder = string.Empty;

    [ObservableProperty]
    private bool _doseFixEnhanceMode = true;

    // ===== RawToDicom Options =====
    [ObservableProperty]
    private string _rawFilePath = string.Empty;

    [ObservableProperty]
    private string _rawFileName = string.Empty;

    [ObservableProperty]
    private string _rawOutputFolder = string.Empty;

    // Patient
    [ObservableProperty]
    private string _rawPatientId = "TEST001";

    [ObservableProperty]
    private string _rawPatientName = "Test^Phantom";

    [ObservableProperty]
    private string _rawPatientSex = "O";

    // Study
    [ObservableProperty]
    private string _rawStudyDate = DateTime.Now.ToString("yyyyMMdd");

    [ObservableProperty]
    private string _rawStudyId = "1";

    [ObservableProperty]
    private string _rawStudyDescription = "Raw Conversion Study";

    // Geometry
    [ObservableProperty]
    private string _rawPixelSpacing = "0.5\\0.5";

    [ObservableProperty]
    private string _rawSliceThickness = "1.0";

    // CT Value
    [ObservableProperty]
    private string _rawRescaleIntercept = "-1024";

    [ObservableProperty]
    private string _rawRescaleSlope = "1";

    [ObservableProperty]
    private string _rawWindowCenter = "40";

    [ObservableProperty]
    private string _rawWindowWidth = "400";

    // Equipment
    [ObservableProperty]
    private string _rawKvp = "120";

    [ObservableProperty]
    private string _rawManufacturer = "DicomToolkits";

    [ObservableProperty]
    private string _rawManufacturerModel = "RawConverter";

    // Raw Conversion State
    [ObservableProperty]
    private bool _rawIsPreviewLoaded;

    [ObservableProperty]
    private bool _rawIsPreviewExpanded;

    [ObservableProperty]
    private bool _rawIsConverting;

    // Raw Preview Data
    [ObservableProperty]
    private string _rawPreviewMin = "-";

    [ObservableProperty]
    private string _rawPreviewMax = "-";

    [ObservableProperty]
    private string _rawPreviewMean = "-";

    [ObservableProperty]
    private string _rawPreviewStdDev = "-";

    [ObservableProperty]
    private string _rawPreviewSlices = "512";

    [ObservableProperty]
    private string _rawPreviewDimensions = "512 x 512 x 512";

    [ObservableProperty]
    private string _rawPreviewFileSize = "-";

    [ObservableProperty]
    private System.Windows.Media.ImageSource? _rawSliceThumbnail;

    [ObservableProperty]
    private int _rawPreviewSliceIndex = 256;  // Default: middle slice

    [ObservableProperty]
    private int _rawSliceStart;  // 0-based, inclusive — first slice to convert

    [ObservableProperty]
    private int _rawSliceEnd = 511;  // 0-based, inclusive — last slice to convert

    // Dynamic max for sliders — set after preview loads (Depth - 1)
    [ObservableProperty]
    private int _rawSliceMax = 511;

    // Total slice count discovered from the file (Depth)
    [ObservableProperty]
    private int _rawTotalSlices = 512;

    partial void OnRawSliceStartChanged(int value)
    {
        if (value < 0) RawSliceStart = 0;
        if (value > RawSliceEnd) RawSliceStart = RawSliceEnd;
        OnPropertyChanged(nameof(RawSliceRangeDisplay));
    }

    partial void OnRawSliceEndChanged(int value)
    {
        if (value < RawSliceStart) RawSliceEnd = RawSliceStart;
        if (value > RawSliceMax) RawSliceEnd = RawSliceMax;
        OnPropertyChanged(nameof(RawSliceRangeDisplay));
    }

    public string RawSliceRangeDisplay => $"{RawSliceStart} – {RawSliceEnd}  ({RawSliceEnd - RawSliceStart + 1} slices)";

    public int RawPreviewSliceMax => RawSliceMax;  // 0-based

    partial void OnRawPreviewSliceIndexChanged(int value)
    {
        if (!RawIsPreviewLoaded) return;
        // Reload slice thumbnail on slider change
        _ = LoadSliceAsync(value);
    }

    private async Task LoadSliceAsync(int sliceIndex)
    {
        if (string.IsNullOrEmpty(RawFilePath) || !System.IO.File.Exists(RawFilePath))
            return;

        try
        {
            var config = BuildRawConfig();
            var sliceBytes = config.Width * config.Height;
            var sliceByteSize = sliceBytes * config.BytesPerPixel;
            var buffer = new byte[sliceByteSize];

            short[]? pixels = await Task.Run(() =>
            {
                using var fs = new FileStream(RawFilePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    sliceByteSize, FileOptions.SequentialScan);
                long offset = config.HeaderOffset + (long)sliceIndex * sliceByteSize;
                fs.Seek(offset, SeekOrigin.Begin);

                int bytesRead = fs.Read(buffer, 0, sliceByteSize);
                if (bytesRead < sliceByteSize) return null;

                short[] result = new short[sliceBytes];
                Buffer.BlockCopy(buffer, 0, result, 0, sliceByteSize);
                return result;
            });

            if (pixels != null)
            {
                RawSliceThumbnail = CreateSliceBitmap(pixels, config.Width, config.Height);
            }
        }
        catch
        {
            // Silently ignore slider-change read errors
        }
    }

    // ===== Classify Options =====
    [ObservableProperty]
    private bool _useYearMonth = true;

    [ObservableProperty]
    private int _selectedNamingRuleIndex;

    [ObservableProperty]
    private bool _enableSortWithinPatient;

    [ObservableProperty]
    private bool _usePatientDateModality;

    [ObservableProperty]
    private bool _useSpecialDateMode;

    [ObservableProperty]
    private int _selectedSpecialDateIndex;

    public List<SpecialDateConfig> SpecialDateOptionList { get; } = SpecialDateOptions.Options;

    public bool UseDirectPatient
    {
        get => !UseYearMonth;
        set => UseYearMonth = !value;
    }

    partial void OnUseYearMonthChanged(bool value) => OnPropertyChanged(nameof(UseDirectPatient));

    partial void OnUsePatientDateModalityChanged(bool value) => OnPropertyChanged(nameof(IsSpecialComboVisible));

    partial void OnUseSpecialDateModeChanged(bool value) => OnPropertyChanged(nameof(IsSpecialComboVisible));

    public bool IsSpecialComboVisible => UsePatientDateModality && UseSpecialDateMode;

    public string[] NamingRules { get; } =
    {
        "PatientID_PatientName",
        "PatientName_PatientID",
        "PatientID",
        "PatientName",
        "Anonymous Sequence (0001, 0002...)",
        "YYYYMMDD (earliest study date)"
    };

    // ===== State =====
    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private bool _hasPreview;

    [ObservableProperty]
    private bool _isPreviewExpanded;

    [ObservableProperty]
    private int _progressCurrent;

    [ObservableProperty]
    private int _progressTotal = 100;

    [ObservableProperty]
    private string _progressStatus = "Ready";

    [ObservableProperty]
    private string _scanSummary = string.Empty;

    [ObservableProperty]
    private string _executionSummary = string.Empty;

    // ===== Preview Data =====
    [ObservableProperty]
    private OperationPlan? _currentPlan;

    public ObservableCollection<FileOperation> PreviewOperations { get; } = new();
    public ObservableCollection<string> LogEntries { get; } = new();

    // ===== Settings Loading/Saving =====

    private void LoadSettings()
    {
        var s = _settingsService.Load();
        SourceFolder = s.SourceFolder ?? string.Empty;
        SortTargetFolder = s.SortTargetFolder ?? string.Empty;
        Statistics.SourceFolder = s.SourceFolder ?? string.Empty;
        SelectedTabIndex = s.SelectedTabIndex;
        SortInPlace = s.SortInPlace;
        UseYearMonth = s.UseYearMonth;
        SelectedNamingRuleIndex = s.SelectedNamingRuleIndex;
        EnableSortWithinPatient = s.EnableSortWithinPatient;
        UsePatientDateModality = s.UsePatientDateModality;
        UseSpecialDateMode = s.UseSpecialDateMode;
        SelectedSpecialDateIndex = s.SelectedSpecialDateIndex;
        DoseFixTargetFolder = s.DoseFixTargetFolder ?? string.Empty;
        DoseFixEnhanceMode = s.DoseFixEnhanceMode;

        // RawToDicom settings
        if (s.RawSourceFilePath != null) RawFilePath = s.RawSourceFilePath;
        if (s.RawOutputFolder != null) RawOutputFolder = s.RawOutputFolder;
        if (s.RawPatientId != null) RawPatientId = s.RawPatientId;
        if (s.RawPatientName != null) RawPatientName = s.RawPatientName;
        if (s.RawPatientSex != null) RawPatientSex = s.RawPatientSex;
        if (s.RawStudyDescription != null) RawStudyDescription = s.RawStudyDescription;
        if (s.RawPixelSpacing != null) RawPixelSpacing = s.RawPixelSpacing;
        if (s.RawSliceThickness != null) RawSliceThickness = s.RawSliceThickness;
        if (s.RawRescaleIntercept != null) RawRescaleIntercept = s.RawRescaleIntercept;
        if (s.RawRescaleSlope != null) RawRescaleSlope = s.RawRescaleSlope;
        if (s.RawWindowCenter != null) RawWindowCenter = s.RawWindowCenter;
        if (s.RawWindowWidth != null) RawWindowWidth = s.RawWindowWidth;
        if (s.RawKvp != null) RawKvp = s.RawKvp;
        if (s.RawSliceStart != null && int.TryParse(s.RawSliceStart, out var ss)) RawSliceStart = ss;
        if (s.RawSliceEnd != null && int.TryParse(s.RawSliceEnd, out var se)) RawSliceEnd = se;

        if (s.SortTargetFolder != null)
            SortTargetFolder = s.SortTargetFolder;

        if (DateTime.TryParse(s.BaseTimeString, out var bt))
            SortBaseTime = bt;
    }

    public void SaveSettings()
    {
        var s = new AppSettings
        {
            SourceFolder = SourceFolder,
            SortTargetFolder = SortTargetFolder,
            IsSortMode = SelectedTabIndex == 0,
            SelectedTabIndex = SelectedTabIndex,
            SortInPlace = SortInPlace,
            UseYearMonth = UseYearMonth,
            SelectedNamingRuleIndex = SelectedNamingRuleIndex,
            EnableSortWithinPatient = EnableSortWithinPatient,
            UsePatientDateModality = UsePatientDateModality,
            UseSpecialDateMode = UseSpecialDateMode,
            SelectedSpecialDateIndex = SelectedSpecialDateIndex,
            DoseFixTargetFolder = DoseFixTargetFolder,
            DoseFixEnhanceMode = DoseFixEnhanceMode,
            // RawToDicom
            RawSourceFilePath = RawFilePath,
            RawOutputFolder = RawOutputFolder,
            RawPatientId = RawPatientId,
            RawPatientName = RawPatientName,
            RawPatientSex = RawPatientSex,
            RawStudyDescription = RawStudyDescription,
            RawPixelSpacing = RawPixelSpacing,
            RawSliceThickness = RawSliceThickness,
            RawRescaleIntercept = RawRescaleIntercept,
            RawRescaleSlope = RawRescaleSlope,
            RawWindowCenter = RawWindowCenter,
            RawWindowWidth = RawWindowWidth,
            RawKvp = RawKvp,
            RawSliceStart = RawSliceStart.ToString(),
            RawSliceEnd = RawSliceEnd.ToString(),
            BaseTimeString = SortBaseTime.ToString("o")
        };
        _settingsService.Save(s);
    }

    // ===== Commands =====

    [RelayCommand]
    private void SelectSourceFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Select Source Folder" };
        if (dialog.ShowDialog() == true)
        {
            SourceFolder = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void SelectSortTargetFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Select Target Folder" };
        if (dialog.ShowDialog() == true)
        {
            SortTargetFolder = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void SelectDoseFixTargetFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Select Target Folder for DoseFix" };
        if (dialog.ShowDialog() == true)
        {
            DoseFixTargetFolder = dialog.FolderName;
        }
    }

    [RelayCommand]
    private async Task ScanAndPreview()
    {
        if (string.IsNullOrEmpty(SourceFolder) || !System.IO.Directory.Exists(SourceFolder))
        {
            LogEntries.Add("Error: Please select a valid source folder.");
            return;
        }

        IsScanning = true;
        HasPreview = false;
        IsPreviewExpanded = false;
        PreviewOperations.Clear();
        CurrentPlan = null;
        ScanSummary = string.Empty;

        var progress = new SimpleProgress(p =>
        {
            ProgressCurrent = p.Current;
            ProgressTotal = p.Total;
            ProgressStatus = p.Status;
        });

        try
        {
            List<DicomFileInfo> files;
            if (SelectedTabIndex == 0) // Sort mode
            {
                string? targetDir = SortInPlace ? null : SortTargetFolder;
                if (!SortInPlace && string.IsNullOrWhiteSpace(SortTargetFolder))
                {
                    LogEntries.Add("Error: Please select a target folder for export.");
                    return;
                }
                progress.Report(new ProgressReport { Current = 0, Total = 1, Status = "Scanning directory (non-recursive)..." });
                files = await _scanService.ScanDirectoryAsync(SourceFolder, progress);
            }
            else // Classify mode
            {
                progress.Report(new ProgressReport { Current = 0, Total = 1, Status = "Scanning directory tree (recursive)..." });
                files = await _scanService.ScanTreeAsync(SourceFolder, progress);
            }

            var validCount = files.Count(f => f.IsValid);
            var invalidCount = files.Count(f => !f.IsValid);
            ScanSummary = $"Found {files.Count} files ({validCount} valid, {invalidCount} unreadable).";

            LogEntries.Add($"[Scan] {ScanSummary}");

            // Build operation plan
            OperationPlan plan;
            if (SelectedTabIndex == 4) // RawToDicom mode — handled separately
            {
                IsScanning = false;
                return;
            }
            else if (SelectedTabIndex == 3) // DoseFix mode
            {
                if (string.IsNullOrWhiteSpace(DoseFixTargetFolder))
                {
                    LogEntries.Add("Error: Please select a target folder for DoseFix output.");
                    return;
                }
                plan = _doseFixService.BuildDoseFixPlan(files, DoseFixTargetFolder, DoseFixEnhanceMode);
            }
            else if (SelectedTabIndex == 0) // Sort mode
            {
                string? targetDir = SortInPlace ? null : SortTargetFolder;
                plan = _sortService.BuildSortPlan(files, targetDir, SortBaseTime);
            }
            else // Classify mode
            {
                var targetRoot = SortTargetFolder;
                if (string.IsNullOrEmpty(targetRoot))
                {
                    targetRoot = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        "DicomToolkits_Classified");
                }

                if (UsePatientDateModality)
                {
                    if (UseSpecialDateMode)
                    {
                        var dateConfig = SpecialDateOptionList[SelectedSpecialDateIndex];
                        plan = _classifyService.BuildClassifyPlanSpecialDateMode(
                            files, targetRoot, EnableSortWithinPatient, dateConfig);
                    }
                    else
                    {
                        plan = _classifyService.BuildClassifyPlanByPatientDateModality(
                            files, targetRoot, EnableSortWithinPatient);
                    }
                }
                else
                {
                    var topStrategy = UseYearMonth
                        ? TopLevelStrategy.YearMonthPatient
                        : TopLevelStrategy.DirectPatient;

                    var namingRule = SelectedNamingRuleIndex switch
                    {
                        0 => PatientFolderNaming.Id_Name,
                        1 => PatientFolderNaming.Name_Id,
                        2 => PatientFolderNaming.IdOnly,
                        3 => PatientFolderNaming.NameOnly,
                        4 => PatientFolderNaming.AnonymousSequence,
                        5 => PatientFolderNaming.DateSequence,
                        _ => PatientFolderNaming.Id_Name
                    };

                    plan = _classifyService.BuildClassifyPlan(files, targetRoot, topStrategy, namingRule, EnableSortWithinPatient);
                }
            }

            CurrentPlan = plan;

            // Populate preview
            foreach (var op in plan.Operations)
            {
                PreviewOperations.Add(op);
            }

            foreach (var w in plan.Warnings)
                LogEntries.Add($"[Warning] {w}");
            foreach (var e in plan.Errors)
                LogEntries.Add($"[Error] {e}");

            HasPreview = true;
            IsPreviewExpanded = true;
            ProgressStatus = $"Preview ready: {plan.Summary}";
        }
        catch (Exception ex)
        {
            LogEntries.Add($"[Error] Scan failed: {ex.Message}");
            ProgressStatus = "Scan failed.";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task ExecuteOperations()
    {
        if (CurrentPlan == null || PreviewOperations.Count == 0)
        {
            LogEntries.Add("Error: Nothing to execute. Run a preview first.");
            return;
        }

        IsExecuting = true;
        ExecutionSummary = string.Empty;

        // Confirmations for Sort mode
        if (SelectedTabIndex == 4) // RawToDicom mode: handled by own commands
        {
            IsExecuting = false;
            return;
        }
        else if (SelectedTabIndex == 3) // DoseFix mode
        {
            if (string.IsNullOrWhiteSpace(DoseFixTargetFolder))
            {
                MessageBox.Show(
                    "Please select a target folder for DoseFix output.",
                    "Target Folder Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);
                IsExecuting = false;
                return;
            }
        }
        else if (SelectedTabIndex == 0)
        {
            if (SortInPlace)
            {
                var result = MessageBox.Show(
                    "In-place mode will rename files and modify timestamps directly in the source folder.\n\nContinue?",
                    "Confirm In-place Operation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes)
                {
                    IsExecuting = false;
                    return;
                }
            }
            else if (string.IsNullOrWhiteSpace(SortTargetFolder))
            {
                MessageBox.Show(
                    "Please select a target folder for export.",
                    "Target Folder Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);
                IsExecuting = false;
                return;
            }
        }

        var progress = new SimpleProgress(p =>
        {
            ProgressCurrent = p.Current;
            ProgressTotal = p.Total;
            ProgressStatus = p.Status;
        });

        try
        {
            var errors = await _executionService.ExecutePlanAsync(CurrentPlan, progress);

            if (errors.Count == 0)
            {
                ExecutionSummary = "All operations completed successfully.";
                LogEntries.Add($"[Success] {ExecutionSummary}");
            }
            else
            {
                ExecutionSummary = $"Completed with {errors.Count} error(s).";
                LogEntries.Add($"[Warning] {ExecutionSummary}");
                foreach (var err in errors)
                {
                    LogEntries.Add($"[Error] {err}");
                }
            }

            ProgressStatus = ExecutionSummary;
        }
        catch (Exception ex)
        {
            LogEntries.Add($"[Error] Execution failed: {ex.Message}");
            ProgressStatus = "Execution failed.";
        }
        finally
        {
            IsExecuting = false;
        }
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogEntries.Clear();
    }

    [RelayCommand]
    private void CancelPreview()
    {
        HasPreview = false;
        IsPreviewExpanded = false;
        PreviewOperations.Clear();
        CurrentPlan = null;
        ProgressStatus = "Cancelled.";
    }

    // ===== RawToDicom Commands =====

    [RelayCommand]
    private void SelectRawFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Raw Volume File",
            Filter = "Raw files (*.raw;*.img;*.bin)|*.raw;*.img;*.bin|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            RawFilePath = dialog.FileName;
            RawFileName = System.IO.Path.GetFileName(dialog.FileName);

            if (string.IsNullOrWhiteSpace(RawOutputFolder))
            {
                var dir = System.IO.Path.GetDirectoryName(dialog.FileName);
                if (dir != null)
                    RawOutputFolder = System.IO.Path.Combine(dir,
                        System.IO.Path.GetFileNameWithoutExtension(dialog.FileName) + "_DICOM");
            }
            LogEntries.Add($"[Raw] Selected file: {RawFileName}");
        }
    }

    [RelayCommand]
    private void SelectRawOutputFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Select Output Folder for DICOM Files" };
        if (dialog.ShowDialog() == true)
        {
            RawOutputFolder = dialog.FolderName;
        }
    }

    [RelayCommand]
    private async Task RawPreview()
    {
        if (string.IsNullOrEmpty(RawFilePath) || !System.IO.File.Exists(RawFilePath))
        {
            LogEntries.Add("Error: Please select a valid raw file.");
            return;
        }

        RawIsPreviewLoaded = false;
        RawIsPreviewExpanded = false;

        LogEntries.Add($"[Raw Preview] Scanning: {RawFileName}...");

        try
        {
            // Build config from current UI values
            var config = BuildRawConfig();

            // Validate file size
            var fileInfo = new System.IO.FileInfo(RawFilePath);
            if (!config.ValidateFileSize(fileInfo.Length))
            {
                var expectedBytes = config.HeaderOffset + config.TotalDataBytes;
                LogEntries.Add($"[Error] File size mismatch. Expected {expectedBytes:N0} bytes, got {fileInfo.Length:N0} bytes.");
                ProgressStatus = "Preview failed: file size mismatch.";
                return;
            }

            // Compute statistics on background thread
            short[]? midPixels = null;
            double min = double.MaxValue, max = double.MinValue;
            double sum = 0, sumSquares = 0;
            long totalPixels = (long)config.Width * config.Height * config.Depth;
            int midSliceIdx = config.Depth / 2;

            await Task.Run(() =>
            {
                var sliceBytes = config.Width * config.Height;
                var sliceByteSize = sliceBytes * config.BytesPerPixel;
                var buffer = new byte[sliceByteSize];

                using var fs = new FileStream(RawFilePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    sliceByteSize, FileOptions.SequentialScan);
                fs.Seek(config.HeaderOffset, SeekOrigin.Begin);

                for (int s = 0; s < config.Depth; s++)
                {
                    int bytesRead = fs.Read(buffer, 0, sliceByteSize);
                    if (bytesRead < sliceByteSize)
                        throw new InvalidOperationException($"Unexpected end of file at slice {s}.");

                    short[] slicePixels = new short[sliceBytes];
                    Buffer.BlockCopy(buffer, 0, slicePixels, 0, sliceByteSize);

                    if (s == midSliceIdx)
                        midPixels = slicePixels;

                    foreach (var v in slicePixels)
                    {
                        double val = v;
                        if (val < min) min = val;
                        if (val > max) max = val;
                        sum += val;
                        sumSquares += val * val;
                    }

                    if ((s + 1) % 100 == 0 || s == config.Depth - 1)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                            ProgressStatus = $"Scanning... {s + 1}/{config.Depth} slices");
                    }
                }
            });

            double mean = sum / totalPixels;
            double variance = (sumSquares / totalPixels) - (mean * mean);
            double stdDev = Math.Sqrt(Math.Max(0, variance));

            RawPreviewMin = min.ToString("F1");
            RawPreviewMax = max.ToString("F1");
            RawPreviewMean = mean.ToString("F1");
            RawPreviewStdDev = stdDev.ToString("F1");
            RawPreviewSlices = config.Depth.ToString();
            RawPreviewDimensions = $"{config.Width} x {config.Height} x {config.Depth}";
            RawPreviewFileSize = $"{fileInfo.Length / (1024.0 * 1024.0):F1} MB";

            // Update slice range bounds from actual file dimensions
            RawTotalSlices = config.Depth;
            RawSliceMax = config.Depth - 1;
            RawPreviewSliceIndex = Math.Min(RawPreviewSliceIndex, RawSliceMax);
            // Clamp conversion range to actual depth
            RawSliceEnd = Math.Min(RawSliceEnd, RawSliceMax);
            RawSliceStart = Math.Min(RawSliceStart, RawSliceEnd);

            // Generate thumbnail from middle slice
            if (midPixels != null)
            {
                RawSliceThumbnail = CreateSliceBitmap(midPixels, config.Width, config.Height);
            }

            RawIsPreviewLoaded = true;
            RawIsPreviewExpanded = true;
            ProgressStatus = $"Preview ready: {config.Depth} slices, mean={mean:F1}";
            LogEntries.Add($"[Raw Preview] {RawPreviewDimensions}, min={RawPreviewMin}, max={RawPreviewMax}, mean={RawPreviewMean}");
            LogEntries.Add($"[Raw Preview] Convert range: {RawSliceRangeDisplay}");
        }
        catch (Exception ex)
        {
            LogEntries.Add($"[Error] Preview failed: {ex.Message}");
            ProgressStatus = "Preview failed.";
        }
    }

    [RelayCommand]
    private async Task RawConvert()
    {
        if (!RawIsPreviewLoaded)
        {
            LogEntries.Add("Error: Please run Preview first to validate the file.");
            return;
        }

        if (string.IsNullOrWhiteSpace(RawOutputFolder))
        {
            LogEntries.Add("Error: Please select an output folder.");
            return;
        }

        RawIsConverting = true;

        var progress = new SimpleProgress(p =>
        {
            ProgressCurrent = p.Current;
            ProgressTotal = p.Total;
            ProgressStatus = p.Status;
        });

        try
        {
            var config = BuildRawConfig();
            LogEntries.Add($"[Raw Convert] Converting slices {config.SliceStart}–{config.SliceEnd} ({config.SliceCount} of {config.Depth} total) to DICOM...");
            LogEntries.Add($"[Raw Convert] Output: {RawOutputFolder}");

            var outputPaths = await _rawToDicomService.ConvertAsync(config, RawOutputFolder, progress);

            LogEntries.Add($"[Success] Converted {outputPaths.Count} DICOM files to: {RawOutputFolder}");
            ProgressStatus = $"Converted {outputPaths.Count} DICOM files.";
        }
        catch (Exception ex)
        {
            LogEntries.Add($"[Error] Conversion failed: {ex.Message}");
            ProgressStatus = "Conversion failed.";
        }
        finally
        {
            RawIsConverting = false;
        }
    }

    [RelayCommand]
    private void RawCancelPreview()
    {
        RawIsPreviewLoaded = false;
        RawIsPreviewExpanded = false;
        RawSliceThumbnail = null;
        ProgressStatus = "Cancelled.";
    }

    private RawConversionConfig BuildRawConfig()
    {
        return new RawConversionConfig
        {
            RawFilePath = RawFilePath,
            PatientId = RawPatientId,
            PatientName = RawPatientName,
            PatientSex = RawPatientSex,
            StudyDate = RawStudyDate,
            StudyId = RawStudyId,
            StudyDescription = RawStudyDescription,
            PixelSpacingRow = TryParseDouble(RawPixelSpacing, '\\', 0, 0.5),
            PixelSpacingCol = TryParseDouble(RawPixelSpacing, '\\', 1, 0.5),
            SliceThickness = TryParseDouble(RawSliceThickness, 1.0),
            RescaleIntercept = TryParseDouble(RawRescaleIntercept, -1024.0),
            RescaleSlope = TryParseDouble(RawRescaleSlope, 1.0),
            WindowCenter = TryParseDouble(RawWindowCenter, 40.0),
            WindowWidth = TryParseDouble(RawWindowWidth, 400.0),
            Kvp = TryParseDouble(RawKvp, 120.0),
            SliceStart = RawSliceStart,
            SliceEnd = RawSliceEnd,
        };
    }

    private static System.Windows.Media.Imaging.WriteableBitmap CreateSliceBitmap(
        short[] pixels, int width, int height)
    {
        if (pixels.Length == 0)
            return new System.Windows.Media.Imaging.WriteableBitmap(
                width, height, 96, 96,
                System.Windows.Media.PixelFormats.Gray8, null);

        double wc = 40.0, ww = 400.0;
        double low = wc - ww / 2.0;
        double high = wc + ww / 2.0;

        byte[] gray8 = new byte[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            double val = pixels[i];
            if (val <= low)
                gray8[i] = 0;
            else if (val >= high)
                gray8[i] = 255;
            else
                gray8[i] = (byte)((val - low) / ww * 255.0);
        }

        var bitmap = new System.Windows.Media.Imaging.WriteableBitmap(
            width, height, 96, 96,
            System.Windows.Media.PixelFormats.Gray8, null);
        bitmap.WritePixels(
            new System.Windows.Int32Rect(0, 0, width, height),
            gray8, width, 0);
        return bitmap;
    }

    private static double TryParseDouble(string value, char separator, int index, double fallback)
    {
        var parts = value.Split(separator);
        if (parts.Length > index && double.TryParse(parts[index],
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var result))
            return result;
        return fallback;
    }

    private static double TryParseDouble(string value, double fallback)
    {
        if (double.TryParse(value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var result))
            return result;
        return fallback;
    }

    /// <summary>
    /// Handle drag-drop folder path. Routes to the active tab.
    /// </summary>
    public void SetSourceFolderFromDrop(string path)
    {
        // RawToDicom mode: accept raw files via drag-drop
        if (SelectedTabIndex == 4)
        {
            if (System.IO.File.Exists(path))
            {
                RawFilePath = path;
                RawFileName = System.IO.Path.GetFileName(path);
                if (string.IsNullOrWhiteSpace(RawOutputFolder))
                {
                    var dir = System.IO.Path.GetDirectoryName(path);
                    if (dir != null)
                        RawOutputFolder = System.IO.Path.Combine(dir,
                            System.IO.Path.GetFileNameWithoutExtension(path) + "_DICOM");
                }
                LogEntries.Add($"[Raw] Drag-dropped file: {RawFileName}");
            }
            return;
        }

        if (!System.IO.Directory.Exists(path)) return;

        if (SelectedTabIndex == 2)
        {
            Statistics.SetSourceFolderFromDrop(path);
        }
        else
        {
            SourceFolder = path;
        }
    }
}
