using System.Diagnostics;

namespace UsageWidget.Core;

/// <summary>Output of one CLI run, or why it produced none.</summary>
public sealed record CliResult(string? Output, string? Error);

/// <summary>Runs a Provider's own CLI (agy, gcloud) and kills its whole process tree on timeout or cancel.</summary>
public sealed class CliRunner(TimeSpan timeout)
{
    /// <param name="workingDirectory">Null runs in the widget's own folder.</param>
    public async Task<CliResult> RunAsync(string exe, IEnumerable<string> args, string? workingDirectory, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
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
            await process.WaitForExitAsync(limit.Token);
        }
        catch (OperationCanceledException)
        {
            return new(null, ct.IsCancellationRequested ? "Cancelled" : $"Timed out after {timeout.TotalSeconds:0}s");
        }

        var output = await stdout;
        var error = await stderr;
        if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output)) return new(output, null);
        var reason = error.Trim().Split('\n').LastOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim();
        return new(null, $"{Path.GetFileName(exe)} exited with code {process.ExitCode}" + (reason is null ? "" : $": {reason}"));
    }

    static void KillTree(Process process)
    {
        try { process.Kill(entireProcessTree: true); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }
}
