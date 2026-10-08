namespace LGMS.Services;

public class ProcessResult
{
    public bool Success => ExitCode == 0;
    public int ExitCode { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public long DurationMs { get; set; }
}

public interface IProcessService
{
    Task<ProcessResult> ExecuteAsync(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null);

    Task<bool> IsCommandAvailableAsync(string command);
}
