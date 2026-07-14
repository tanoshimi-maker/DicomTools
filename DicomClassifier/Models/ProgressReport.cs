namespace DicomClassifier.Models;

/// <summary>
/// Progress report data for file scanning and execution operations.
/// </summary>
public class ProgressReport
{
    public int Current { get; set; }
    public int Total { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Simple progress reporter to avoid dependency on System.Progress{T}.
/// </summary>
public class SimpleProgress : IProgress<ProgressReport>
{
    private readonly Action<ProgressReport> _handler;

    public SimpleProgress(Action<ProgressReport> handler)
    {
        _handler = handler;
    }

    public void Report(ProgressReport value)
    {
        _handler?.Invoke(value);
    }
}
