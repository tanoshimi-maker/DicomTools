using PMTALL.Models;

namespace PMTALL.Services;

/// <summary>
/// Service for executing file operations plan.
/// </summary>
public interface IExecutionService
{
    /// <summary>
    /// Execute all operations in the plan, reporting progress.
    /// Returns list of errors encountered.
    /// </summary>
    Task<List<string>> ExecutePlanAsync(
        OperationPlan plan,
        IProgress<ProgressReport>? progress = null);
}
