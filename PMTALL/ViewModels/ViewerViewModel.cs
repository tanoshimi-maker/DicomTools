using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PMTALL.Helpers;
using PMTALL.Models;
using PMTALL.Services;
using Microsoft.Win32;

namespace PMTALL.ViewModels;

public partial class ViewerViewModel : ObservableObject
{
    private readonly ICtViewerService _viewerService;
    private List<DicomFileInfo> _cachedFiles = new();

    public ViewerViewModel(ICtViewerService viewerService)
    {
        _viewerService = viewerService;

        // Wire up slot callbacks so slider/window changes trigger bitmap refresh
        SlotA.OnAxialChanged = s => RefreshAxialBitmap(s);
        SlotA.OnCoronalChanged = s => RefreshCoronalBitmap(s);
        SlotA.OnSagittalChanged = s => RefreshSagittalBitmap(s);
        SlotA.OnWindowLevelChanged = s => RefreshSlotBitmaps(s);

        SlotB.OnAxialChanged = s => RefreshAxialBitmap(s);
        SlotB.OnCoronalChanged = s => RefreshCoronalBitmap(s);
        SlotB.OnSagittalChanged = s => RefreshSagittalBitmap(s);
        SlotB.OnWindowLevelChanged = s => RefreshSlotBitmaps(s);

        _switchToFolderView();
    }

    // ===== Source Folder =====

    [ObservableProperty]
    private string _sourceFolder = string.Empty;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanStatus = "Ready";

    [ObservableProperty]
    private int _progressCurrent;

    [ObservableProperty]
    private int _progressTotal = 100;

    // ===== Tree View =====

    [ObservableProperty]
    private bool _isFolderView = true;

    [ObservableProperty]
    private bool _isDicomHierarchyView;

    partial void OnIsFolderViewChanged(bool value)
    {
        if (value) RebuildTree();
    }

    partial void OnIsDicomHierarchyViewChanged(bool value)
    {
        if (value) RebuildTree();
    }

    public ObservableCollection<DicomTreeNode> TreeNodes { get; } = new();

    // ===== Viewer Slots =====

    public ViewerSlotState SlotA { get; } = new() { Label = "Series A" };
    public ViewerSlotState SlotB { get; } = new() { Label = "Series B" };

    [ObservableProperty]
    private bool _hasAnySeriesLoaded;

    // ===== Window/Level Presets =====

    public List<WindowLevelPreset> Presets { get; } = WindowLevelPresets.All;

    [ObservableProperty]
    private int _selectedPresetIndex;

    // ===== Log =====

    public ObservableCollection<string> LogEntries { get; } = new();

    // ===== Commands =====

    [RelayCommand]
    private void SelectSourceFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Select Folder with DICOM Files" };
        if (dialog.ShowDialog() == true)
        {
            SourceFolder = dialog.FolderName;
        }
    }

    [RelayCommand]
    private async Task ScanAndBuildTree()
    {
        if (string.IsNullOrEmpty(SourceFolder) || !Directory.Exists(SourceFolder))
        {
            LogEntries.Add("Error: Please select a valid source folder.");
            return;
        }

        IsScanning = true;
        TreeNodes.Clear();
        _cachedFiles.Clear();
        ScanStatus = "Scanning...";

        var progress = new SimpleProgress(p =>
        {
            ProgressCurrent = p.Current;
            ProgressTotal = p.Total;
            ScanStatus = p.Status;
        });

        try
        {
            LogEntries.Add($"[Scan] Scanning: {SourceFolder}");
            var files = await _viewerService.ScanFolderAsync(SourceFolder, progress);
            _cachedFiles = files;

            var validCount = files.Count(f => f.IsValid);
            var ctMrCount = files.Count(f => f.IsValid && f.Modality is "CT" or "MR");
            var invalidCount = files.Count(f => !f.IsValid);

            LogEntries.Add($"[Scan] Found {files.Count} files: {validCount} valid ({ctMrCount} CT/MR), {invalidCount} unreadable.");
            ScanStatus = $"Found {ctMrCount} CT/MR files in {files.Count} total.";

            // Build the active tree
            RebuildTree();
        }
        catch (Exception ex)
        {
            LogEntries.Add($"[Error] Scan failed: {ex.Message}");
            ScanStatus = "Scan failed.";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void _switchToFolderView()
    {
        IsFolderView = true;
        IsDicomHierarchyView = false;
    }

    [RelayCommand]
    private void _switchToDicomView()
    {
        IsFolderView = false;
        IsDicomHierarchyView = true;
    }

    private void RebuildTree()
    {
        TreeNodes.Clear();
        if (_cachedFiles.Count == 0) return;

        var nodes = IsFolderView
            ? _viewerService.BuildFolderTree(_cachedFiles)
            : _viewerService.BuildDicomHierarchyTree(_cachedFiles);

        foreach (var node in nodes)
            TreeNodes.Add(node);
    }

    /// <summary>
    /// Generic handler for tree node double-click. Routes SeriesTreeNode to Slot A (or B if A loaded).
    /// </summary>
    [RelayCommand]
    private async Task _loadSeriesFromTree(DicomTreeNode? node)
    {
        if (node is not SeriesTreeNode seriesNode) return;
        if (!seriesNode.IsViewable || seriesNode.Files.Count == 0) return;

        // If Slot A is empty or both are loaded, load into A. Otherwise load into B.
        if (!SlotA.IsLoaded || SlotB.IsLoaded)
        {
            await LoadSeriesIntoSlot(SlotA, seriesNode);
        }
        else
        {
            await LoadSeriesIntoSlot(SlotB, seriesNode);
        }
    }

    private async Task LoadSeriesIntoSlot(ViewerSlotState slot, SeriesTreeNode node)
    {
        slot.Clear();
        ScanStatus = $"Loading {node.Modality} series into {slot.Label}...";

        LogEntries.Add($"[{slot.Label}] Loading {node.SliceCount} slices of {node.Modality} series...");

        var progress = new SimpleProgress(p =>
        {
            ProgressCurrent = p.Current;
            ProgressTotal = p.Total;
            ScanStatus = p.Status;
        });

        try
        {
            var volume = await _viewerService.LoadVolumeAsync(node.SeriesInstanceUid, node.Files, progress);

            if (!volume.IsViewable || volume.Depth == 0)
            {
                LogEntries.Add($"[{slot.Label}] Error: Series has no readable slices.");
                ScanStatus = "Load failed.";
                return;
            }

            slot.Volume = volume;
            slot.MaxAxialSlices = volume.Depth - 1;
            slot.MaxCoronalSlices = volume.Rows - 1;
            slot.MaxSagittalSlices = volume.Columns - 1;

            // Set default indices to the middle of each dimension
            slot.AxialSliceIndex = volume.Depth / 2;
            slot.CoronalSliceIndex = volume.Rows / 2;
            slot.SagittalSliceIndex = volume.Columns / 2;

            // Apply initial window/level from DICOM or preset
            var midSlice = volume.Slices[slot.AxialSliceIndex];
            slot.WindowCenter = midSlice.WindowCenter;
            slot.WindowWidth = midSlice.WindowWidth;

            slot.SeriesInfo = $"{volume.Modality} | {volume.SeriesDescription} | {volume.Depth} slices";
            slot.IsLoaded = true;
            HasAnySeriesLoaded = SlotA.IsLoaded || SlotB.IsLoaded;

            // Generate initial bitmaps
            RefreshSlotBitmaps(slot);

            LogEntries.Add($"[{slot.Label}] Loaded: {slot.SeriesInfo}");
            LogEntries.Add($"[{slot.Label}] Dimensions: {volume.Columns}x{volume.Rows}x{volume.Depth}");
            LogEntries.Add($"[{slot.Label}] Window: center={slot.WindowCenter:F0}, width={slot.WindowWidth:F0}");
            ScanStatus = $"{slot.Label} loaded.";
        }
        catch (Exception ex)
        {
            LogEntries.Add($"[{slot.Label}] Error loading series: {ex.Message}");
            ScanStatus = "Load failed.";
        }
    }

    [RelayCommand]
    private void ClearSlotA()
    {
        SlotA.Clear();
        HasAnySeriesLoaded = SlotB.IsLoaded;
        LogEntries.Add("[Slot A] Cleared.");
        ScanStatus = "Slot A cleared.";
    }

    [RelayCommand]
    private void ClearSlotB()
    {
        SlotB.Clear();
        HasAnySeriesLoaded = SlotA.IsLoaded;
        LogEntries.Add("[Slot B] Cleared.");
        ScanStatus = "Slot B cleared.";
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogEntries.Clear();
    }

    /// <summary>
    /// Apply the selected window/level preset to the active slot.
    /// </summary>
    [RelayCommand]
    private void ApplyPreset()
    {
        if (SelectedPresetIndex < 0 || SelectedPresetIndex >= Presets.Count) return;
        var preset = Presets[SelectedPresetIndex];
        if (preset.Name == "Manual") return;

        // Apply to the most recently loaded slot, or whichever is active
        // For simplicity, apply to both loaded slots
        if (SlotA.IsLoaded)
        {
            SlotA.WindowCenter = preset.Center;
            SlotA.WindowWidth = preset.Width;
            RefreshSlotBitmaps(SlotA);
        }
        if (SlotB.IsLoaded)
        {
            SlotB.WindowCenter = preset.Center;
            SlotB.WindowWidth = preset.Width;
            RefreshSlotBitmaps(SlotB);
        }

        LogEntries.Add($"[W/L] Applied preset: {preset.Name} (C={preset.Center}, W={preset.Width})");
    }

    // ===== Bitmap Generation =====

    /// <summary>
    /// Refresh all three bitmaps for a slot (axial, coronal, sagittal).
    /// </summary>
    public void RefreshSlotBitmaps(ViewerSlotState slot)
    {
        if (!slot.IsLoaded || slot.Volume == null) return;

        var volume = slot.Volume;

        // --- Axial ---
        if (slot.AxialSliceIndex >= 0 && slot.AxialSliceIndex < volume.Depth)
        {
            var slice = volume.Slices[slot.AxialSliceIndex];
            slot.AxialBitmap = WindowLevelHelper.CreateBitmap(
                slice.Pixels, volume.Columns, volume.Rows,
                slot.WindowCenter, slot.WindowWidth,
                slice.RescaleIntercept, slice.RescaleSlope);
        }

        // --- Coronal ---
        if (slot.CoronalSliceIndex >= 0 && slot.CoronalSliceIndex < volume.Rows)
        {
            var coronalPixels = MprReconstructor.BuildCoronalPlane(volume, slot.CoronalSliceIndex);
            if (coronalPixels.Length > 0)
            {
                var midSlice = volume.Slices[Math.Min(volume.Depth / 2, volume.Depth - 1)];
                slot.CoronalBitmap = WindowLevelHelper.CreateBitmap(
                    coronalPixels, volume.Columns, volume.Depth,
                    slot.WindowCenter, slot.WindowWidth,
                    midSlice.RescaleIntercept, midSlice.RescaleSlope);
            }
        }

        // --- Sagittal ---
        if (slot.SagittalSliceIndex >= 0 && slot.SagittalSliceIndex < volume.Columns)
        {
            var sagittalPixels = MprReconstructor.BuildSagittalPlane(volume, slot.SagittalSliceIndex);
            if (sagittalPixels.Length > 0)
            {
                var midSlice = volume.Slices[Math.Min(volume.Depth / 2, volume.Depth - 1)];
                slot.SagittalBitmap = WindowLevelHelper.CreateBitmap(
                    sagittalPixels, volume.Rows, volume.Depth,
                    slot.WindowCenter, slot.WindowWidth,
                    midSlice.RescaleIntercept, midSlice.RescaleSlope);
            }
        }
    }

    /// <summary>
    /// Refresh only the axial bitmap (fast path).
    /// </summary>
    public void RefreshAxialBitmap(ViewerSlotState slot)
    {
        if (!slot.IsLoaded || slot.Volume == null) return;
        if (slot.AxialSliceIndex < 0 || slot.AxialSliceIndex >= slot.Volume.Depth) return;

        var slice = slot.Volume.Slices[slot.AxialSliceIndex];
        slot.AxialBitmap = WindowLevelHelper.CreateBitmap(
            slice.Pixels, slot.Volume.Columns, slot.Volume.Rows,
            slot.WindowCenter, slot.WindowWidth,
            slice.RescaleIntercept, slice.RescaleSlope);
    }

    /// <summary>
    /// Refresh only the coronal bitmap (fast path).
    /// </summary>
    public void RefreshCoronalBitmap(ViewerSlotState slot)
    {
        if (!slot.IsLoaded || slot.Volume == null) return;
        if (slot.CoronalSliceIndex < 0 || slot.CoronalSliceIndex >= slot.Volume.Rows) return;

        var coronalPixels = MprReconstructor.BuildCoronalPlane(slot.Volume, slot.CoronalSliceIndex);
        if (coronalPixels.Length == 0) return;

        var midSlice = slot.Volume.Slices[Math.Min(slot.Volume.Depth / 2, slot.Volume.Depth - 1)];
        slot.CoronalBitmap = WindowLevelHelper.CreateBitmap(
            coronalPixels, slot.Volume.Columns, slot.Volume.Depth,
            slot.WindowCenter, slot.WindowWidth,
            midSlice.RescaleIntercept, midSlice.RescaleSlope);
    }

    /// <summary>
    /// Refresh only the sagittal bitmap (fast path).
    /// </summary>
    public void RefreshSagittalBitmap(ViewerSlotState slot)
    {
        if (!slot.IsLoaded || slot.Volume == null) return;
        if (slot.SagittalSliceIndex < 0 || slot.SagittalSliceIndex >= slot.Volume.Columns) return;

        var sagittalPixels = MprReconstructor.BuildSagittalPlane(slot.Volume, slot.SagittalSliceIndex);
        if (sagittalPixels.Length == 0) return;

        var midSlice = slot.Volume.Slices[Math.Min(slot.Volume.Depth / 2, slot.Volume.Depth - 1)];
        slot.SagittalBitmap = WindowLevelHelper.CreateBitmap(
            sagittalPixels, slot.Volume.Rows, slot.Volume.Depth,
            slot.WindowCenter, slot.WindowWidth,
            midSlice.RescaleIntercept, midSlice.RescaleSlope);
    }

    /// <summary>
    /// Handle drag-drop folder path.
    /// </summary>
    public void SetSourceFolderFromDrop(string path)
    {
        if (Directory.Exists(path))
        {
            SourceFolder = path;
        }
    }
}
