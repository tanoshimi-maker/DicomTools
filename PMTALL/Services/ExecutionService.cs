using PMTALL.Models;

namespace PMTALL.Services;

public class ExecutionService : IExecutionService
{
    private readonly IDoseFixService _doseFixService;

    public ExecutionService(IDoseFixService doseFixService)
    {
        _doseFixService = doseFixService;
    }

    public async Task<List<string>> ExecutePlanAsync(
        OperationPlan plan,
        IProgress<ProgressReport>? progress = null)
    {
        var errors = new List<string>();
        var operations = plan.Operations.Where(o => o.Type != OperationType.CreateDirectory).ToList();
        var dirOps = plan.Operations.Where(o => o.Type == OperationType.CreateDirectory).ToList();

        int total = operations.Count + dirOps.Count;
        int completed = 0;

        // Create directories first
        foreach (var op in dirOps)
        {
            try
            {
                Directory.CreateDirectory(op.DestinationPath);
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to create directory {op.DestinationPath}: {ex.Message}");
            }
            completed++;
            progress?.Report(new ProgressReport
            {
                Current = completed,
                Total = total,
                Status = $"Creating directories... {completed}/{total}"
            });
        }

        await Task.Run(() =>
        {
            foreach (var op in operations)
            {
                try
                {
                    ExecuteSingleOperation(op);
                }
                catch (Exception ex)
                {
                    errors.Add($"Operation failed: {op.Description} - {ex.Message}");
                }

                completed++;
                if ((completed - dirOps.Count) % 5 == 0 || completed == total)
                {
                    progress?.Report(new ProgressReport
                    {
                        Current = completed,
                        Total = total,
                        Status = $"Executing... {completed - dirOps.Count}/{operations.Count}"
                    });
                }
            }
        });

        return errors;
    }

    private void ExecuteSingleOperation(FileOperation op)
    {
        switch (op.Type)
        {
            case OperationType.Copy:
                var copyDir = Path.GetDirectoryName(op.DestinationPath);
                if (copyDir != null) Directory.CreateDirectory(copyDir);
                File.Copy(op.SourcePath!, op.DestinationPath, overwrite: true);
                if (op.NewLastWriteTime.HasValue)
                {
                    File.SetLastWriteTime(op.DestinationPath, op.NewLastWriteTime.Value);
                }
                break;

            case OperationType.Move:
                var moveDir = Path.GetDirectoryName(op.DestinationPath);
                if (moveDir != null) Directory.CreateDirectory(moveDir);
                if (File.Exists(op.SourcePath!))
                {
                    File.Move(op.SourcePath!, op.DestinationPath, overwrite: true);
                }
                break;

            case OperationType.Rename:
                var renameDir = Path.GetDirectoryName(op.DestinationPath);
                if (renameDir != null) Directory.CreateDirectory(renameDir);
                if (File.Exists(op.SourcePath!))
                {
                    File.Move(op.SourcePath!, op.DestinationPath, overwrite: false);
                }
                break;

            case OperationType.ChangeTime:
                if (File.Exists(op.DestinationPath))
                {
                    File.SetLastWriteTime(op.DestinationPath, op.NewLastWriteTime!.Value);
                }
                break;

            case OperationType.CreateDirectory:
                Directory.CreateDirectory(op.DestinationPath);
                break;

            case OperationType.DoseFix:
                if (op.SourcePath != null && op.FixIop != null && op.FixIpp != null)
                {
                    var destDir = Path.GetDirectoryName(op.DestinationPath);
                    if (destDir != null) Directory.CreateDirectory(destDir);
                    _doseFixService.FixDoseFile(op.SourcePath, op.DestinationPath, op.FixIop, op.FixIpp);
                }
                break;
        }
    }
}
