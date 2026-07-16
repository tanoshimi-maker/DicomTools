using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PMTALL.Models;

/// <summary>
/// Tree node for the DICOM Viewer folder/DICOM-hierarchy tree.
/// Supports collapsible/expandable TreeView with observable properties.
/// </summary>
public partial class DicomTreeNode : ObservableObject
{
    [ObservableProperty]
    private string _displayName = "";

    [ObservableProperty]
    private string _detailText = "";

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Node type for HierarchicalDataTemplate selection: Folder / Patient / Study / Series / File</summary>
    [ObservableProperty]
    private string _nodeType = "";

    public ObservableCollection<DicomTreeNode> Children { get; } = new();
}

/// <summary>
/// A series-level tree node that can be loaded into the viewer.
/// </summary>
public class SeriesTreeNode : DicomTreeNode
{
    public string SeriesInstanceUid { get; set; } = "";
    public string Modality { get; set; } = "";
    public int SliceCount { get; set; }
    public List<DicomFileInfo> Files { get; set; } = new();

    public bool IsViewable => Modality is "CT" or "MR";
}
