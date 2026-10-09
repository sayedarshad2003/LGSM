using System.Diagnostics;

namespace LGMS.Services;

public sealed class ProcessExecutionResult
{
    public int ExitCode { get; init; }
    public string Output { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public bool Success => ExitCode == 0;
}

public sealed class ProcessRunner
{
    public async Task<ProcessExecutionResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory)
                ? Environment.CurrentDirectory
                : workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeoutCts = new CancellationTokenSource(timeout ?? TimeSpan.FromMinutes(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
            return new ProcessExecutionResult
            {
                ExitCode = process.ExitCode,
                Output = (await outputTask).Trim(),
                Error = (await errorTask).Trim()
            };
        }
        catch
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignore failure to kill already-exited processes
            }
            throw;
        }
    }
}
