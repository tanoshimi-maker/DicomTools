using PMTALL.Helpers;
using PMTALL.Models;

namespace PMTALL.Services;

public class SortService : ISortService
{
    private static readonly DateTime DefaultBaseTime = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Local);

    public OperationPlan BuildSortPlan(
        List<DicomFileInfo> files,
        string? targetDirectory = null,
        DateTime? baseTime = null)
    {
        var plan = new OperationPlan();
        DateTime baseTimeVal = baseTime ?? DefaultBaseTime;

        // Filter valid files with timestamps
        var validFiles = files
            .Where(f => f.IsValid && f.AcquisitionTimestamp.HasValue)
            .OrderBy(f => f.AcquisitionTimestamp!.Value)
            .ToList();

        var invalidFiles = files
            .Where(f => !f.IsValid || !f.AcquisitionTimestamp.HasValue)
            .ToList();

        foreach (var invalid in invalidFiles)
        {
            plan.Warnings.Add($"[{invalid.FileName}] Missing timestamp or invalid - will be moved to Unclassified");
        }

        bool isInPlace = string.IsNullOrEmpty(targetDirectory);
        plan.IsInPlace = isInPlace;
        string workDir = isInPlace ? Path.GetDirectoryName(files.First().SourcePath)! : targetDirectory!;

        // Ensure target directory exists
        if (!isInPlace)
        {
            plan.Operations.Add(new FileOperation
            {
                Type = OperationType.CreateDirectory,
                DestinationPath = workDir
            });
        }

        // Also create Unclassified folder
        var unclassifiedDir = Path.Combine(workDir, "_Unclassified");
        plan.Operations.Add(new FileOperation
        {
            Type = OperationType.CreateDirectory,
            DestinationPath = unclassifiedDir
        });

        int totalCount = validFiles.Count + invalidFiles.Count;

        for (int i = 0; i < validFiles.Count; i++)
        {
            var file = validFiles[i];
            var prefix = FileNameHelper.GetAlphaPrefix(i, validFiles.Count);
            var newFileName = prefix + file.FileName;
            var destPath = Path.Combine(workDir, newFileName);
            var newTime = baseTimeVal.AddSeconds(i);

            if (isInPlace)
            {
                // Rename
                if (!file.FileName.Equals(newFileName, StringComparison.OrdinalIgnoreCase))
                {
                    plan.Operations.Add(new FileOperation
                    {
                        Type = OperationType.Rename,
                        SourcePath = file.SourcePath,
                        DestinationPath = destPath,
                        OldName = file.FileName,
                        NewName = newFileName
                    });
                }

                // Change time
                plan.Operations.Add(new FileOperation
                {
                    Type = OperationType.ChangeTime,
                    SourcePath = destPath,
                    DestinationPath = destPath,
                    OldName = newFileName,
                    NewLastWriteTime = newTime
                });
            }
            else
            {
                // Copy to target with new name
                plan.Operations.Add(new FileOperation
                {
                    Type = OperationType.Copy,
                    SourcePath = file.SourcePath,
                    DestinationPath = destPath,
                    NewLastWriteTime = newTime
                });
            }
        }

        // Handle invalid files - move/copy to Unclassified
        foreach (var invalid in invalidFiles)
        {
            var destPath = Path.Combine(unclassifiedDir, invalid.FileName);
            if (isInPlace)
            {
                plan.Operations.Add(new FileOperation
                {
                    Type = OperationType.Move,
                    SourcePath = invalid.SourcePath,
                    DestinationPath = destPath,
                    OldName = invalid.FileName,
                    NewName = invalid.FileName
                });
            }
            else
            {
                plan.Operations.Add(new FileOperation
                {
                    Type = OperationType.Copy,
                    SourcePath = invalid.SourcePath,
                    DestinationPath = destPath
                });
            }
        }

        plan.FileCount = totalCount;

        // Deduplicate directory creations
        plan.Operations = plan.Operations
            .GroupBy(o => $"{o.Type}:{o.DestinationPath}")
            .Select(g => g.First())
            .ToList();

        return plan;
    }
}
