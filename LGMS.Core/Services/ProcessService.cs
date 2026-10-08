using System.Diagnostics;

namespace LGMS.Services;

public class ProcessService : IProcessService
{
    private readonly ILogger<ProcessService> _logger;

    public ProcessService(ILogger<ProcessService> logger)
    {
        _logger = logger;
    }

    public async Task<ProcessResult> ExecuteAsync(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var processResult = new ProcessResult();
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(60);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(effectiveTimeout);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = Directory.Exists(workingDirectory) ? workingDirectory : Environment.CurrentDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            foreach (var arg in arguments)
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var process = new Process { StartInfo = startInfo };

            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token);

            processResult.StandardOutput = await stdoutTask;
            processResult.StandardError = await stderrTask;
            processResult.ExitCode = process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            processResult.ExitCode = -1;
            processResult.StandardError = "Operation was cancelled or timed out.";
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Process execution failed or executable not found: {Executable}", executable);
            processResult.ExitCode = -2;
            processResult.StandardError = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            processResult.DurationMs = stopwatch.ElapsedMilliseconds;
        }

        return processResult;
    }

    public async Task<bool> IsCommandAvailableAsync(string command)
    {
        try
        {
            var result = await ExecuteAsync(command, new[] { "--version" }, Environment.CurrentDirectory, timeout: TimeSpan.FromSeconds(3));
            return result.Success;
        }
        catch
        {
            return false;
        }
    }
}
