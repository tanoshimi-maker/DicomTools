using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PMTALL.Models;

/// <summary>
/// Per-viewer-slot state: slice indices, window/level, rendered bitmaps.
/// Each slot (A or B) owns one of these for the series loaded into it.
/// Bitmap refresh is delegated to the parent ViewerViewModel via callback.
/// </summary>
public partial class ViewerSlotState : ObservableObject
{
    /// <summary>Callback invoked when any slice index or window/level changes. The slot passes itself.</summary>
    public Action<ViewerSlotState>? OnStateChanged { get; set; }

    /// <summary>Callback invoked only when an axial slice index changes (fast path).</summary>
    public Action<ViewerSlotState>? OnAxialChanged { get; set; }

    /// <summary>Callback invoked only when coronal slice index changes.</summary>
    public Action<ViewerSlotState>? OnCoronalChanged { get; set; }

    /// <summary>Callback invoked only when sagittal slice index changes.</summary>
    public Action<ViewerSlotState>? OnSagittalChanged { get; set; }

    /// <summary>Callback invoked when window center or width changes.</summary>
    public Action<ViewerSlotState>? OnWindowLevelChanged { get; set; }

    [ObservableProperty]
    private string _label = "";

    [ObservableProperty]
    private bool _isLoaded;

    [ObservableProperty]
    private CtVolume? _volume;

    // === Current Slice Indices ===

    [ObservableProperty]
    private int _axialSliceIndex;

    [ObservableProperty]
    private int _coronalSliceIndex;

    [ObservableProperty]
    private int _sagittalSliceIndex;

    // === Maximum Slice Values ===

    [ObservableProperty]
    private int _maxAxialSlices;

    [ObservableProperty]
    private int _maxCoronalSlices;

    [ObservableProperty]
    private int _maxSagittalSlices;

    // === Window / Level ===

    [ObservableProperty]
    private double _windowCenter = 40;

    [ObservableProperty]
    private double _windowWidth = 400;

    // === Rendered Bitmaps ===

    [ObservableProperty]
    private WriteableBitmap? _axialBitmap;

    [ObservableProperty]
    private WriteableBitmap? _coronalBitmap;

    [ObservableProperty]
    private WriteableBitmap? _sagittalBitmap;

    // === Series Info for Display ===

    [ObservableProperty]
    private string _seriesInfo = "";

    /// <summary>Clear all volume data and bitmaps to free memory.</summary>
    public void Clear()
    {
        Volume = null;
        IsLoaded = false;
        AxialBitmap = null;
        CoronalBitmap = null;
        SagittalBitmap = null;
        AxialSliceIndex = 0;
        CoronalSliceIndex = 0;
        SagittalSliceIndex = 0;
        MaxAxialSlices = 0;
        MaxCoronalSlices = 0;
        MaxSagittalSlices = 0;
        SeriesInfo = "";
    }

    partial void OnAxialSliceIndexChanged(int value)
    {
        OnAxialChanged?.Invoke(this);
    }

    partial void OnCoronalSliceIndexChanged(int value)
    {
        OnCoronalChanged?.Invoke(this);
    }

    partial void OnSagittalSliceIndexChanged(int value)
    {
        OnSagittalChanged?.Invoke(this);
    }

    partial void OnWindowCenterChanged(double value)
    {
        OnWindowLevelChanged?.Invoke(this);
    }

    partial void OnWindowWidthChanged(double value)
    {
        OnWindowLevelChanged?.Invoke(this);
    }
}
