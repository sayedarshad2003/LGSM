namespace LGMS.Models;

public class GitResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public long DurationMs { get; set; }

    public static GitResult Ok(string output, long durationMs = 0) => new()
    {
        Success = true,
        ExitCode = 0,
        Output = output,
        DurationMs = durationMs
    };

    public static GitResult Fail(string error, int exitCode = 1, long durationMs = 0) => new()
    {
        Success = false,
        ExitCode = exitCode,
        Error = error,
        DurationMs = durationMs
    };
}
