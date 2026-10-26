using System.Diagnostics;

namespace UsageWidget.Core;

/// <summary>Output of one CLI run, or why it produced none; the exit code is null when the CLI never finished.</summary>
public sealed record CliResult(string? Output, string? Error, int? ExitCode = null);

/// <summary>Runs a command-line tool; the seam the readers and the Keychain store use.</summary>
public interface ICommandRunner
{
    /// <param name="workingDirectory">Null runs in the widget's own folder.</param>
    /// <param name="input">Text written to the tool's standard input, then closed; null leaves it unused.</param>
    Task<CliResult> RunAsync(string exe, IEnumerable<string> args, string? workingDirectory, CancellationToken ct, string? input = null);
}

/// <summary>Runs a Provider's own CLI (agy, gcloud) or a system tool and kills its whole process tree on timeout or cancel.</summary>
public sealed class CliRunner(TimeSpan timeout) : ICommandRunner
{
    public async Task<CliResult> RunAsync(string exe, IEnumerable<string> args, string? workingDirectory, CancellationToken ct, string? input = null)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = input is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        if (workingDirectory is not null) psi.WorkingDirectory = workingDirectory;
        psi.Environment["PATH"] = SystemPath.Current();

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new(null, $"Could not start {Path.GetFileName(exe)}: {e.Message}");
        }

        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(timeout);
        // Kill synchronously inside Cancel(), so an app exit cannot outrun it and orphan the CLI.
        using var kill = limit.Token.Register(() => KillTree(process));
        try
        {
            if (input is not null)
            {
                await process.StandardInput.WriteAsync(input.AsMemory(), limit.Token);
                process.StandardInput.Close();
            }
            await process.WaitForExitAsync(limit.Token);
        }
        catch (OperationCanceledException)
        {
            return new(null, ct.IsCancellationRequested ? "Cancelled" : $"Timed out after {timeout.TotalSeconds:0}s");
        }

        var output = await stdout;
        var error = await stderr;
        if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output)) return new(output, null, 0);
        var reason = error.Trim().Split('\n').LastOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim();
        return new(null, $"{Path.GetFileName(exe)} exited with code {process.ExitCode}" + (reason is null ? "" : $": {reason}"), process.ExitCode);
    }

    static void KillTree(Process process)
    {
        try { process.Kill(entireProcessTree: true); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }
}
