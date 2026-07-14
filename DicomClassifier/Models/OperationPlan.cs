namespace DicomClassifier.Models;

/// <summary>
/// Complete operation plan containing all file operations to execute.
/// </summary>
public class OperationPlan
{
    public List<FileOperation> Operations { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public int FileCount { get; set; }
    public int DirectoryCount { get; set; }
    public bool IsInPlace { get; set; }

    public string Summary => $"{Operations.Count} operations ({FileCount} files, {DirectoryCount} directories)" +
                             (IsInPlace ? " [In-place]" : " [Copy mode]") +
                             (Warnings.Count > 0 ? $", {Warnings.Count} warnings" : "") +
                             (Errors.Count > 0 ? $", {Errors.Count} errors" : "");
}
