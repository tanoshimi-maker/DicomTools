namespace PMTALL.Models;

/// <summary>
/// Type of file operation to be performed.
/// </summary>
public enum OperationType
{
    Copy,
    Move,
    Rename,
    ChangeTime,
    CreateDirectory,
    DoseFix
}

/// <summary>
/// Represents a single file/directory operation in the execution plan.
/// </summary>
public class FileOperation
{
    public OperationType Type { get; set; }
    public string? SourcePath { get; set; }
    public string DestinationPath { get; set; } = string.Empty;
    public DateTime? NewLastWriteTime { get; set; }
    public string? OldName { get; set; }
    public string? NewName { get; set; }

    // DoseFix parameters
    public double[]? FixIop { get; set; }
    public double[]? FixIpp { get; set; }

    public string Description => Type switch
    {
        OperationType.Copy => $"Copy: {SourcePath} → {DestinationPath}",
        OperationType.Move => $"Move: {SourcePath} → {DestinationPath}",
        OperationType.Rename => $"Rename: {OldName} → {NewName}",
        OperationType.ChangeTime => $"Change time: {OldName} → {NewLastWriteTime:yyyy-MM-dd HH:mm:ss}",
        OperationType.CreateDirectory => $"Create folder: {DestinationPath}",
        OperationType.DoseFix => $"Fix RTDOSE: {SourcePath} → {DestinationPath}",
        _ => ToString() ?? "Unknown operation"
    };
}
