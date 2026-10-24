using System.Diagnostics;

namespace UsageWidget.Core;

/// <summary>Runs codexbar-cli once per Account (ADR 0001).</summary>
public sealed class UsageReader(string codexbarPath, TimeSpan timeout)
{
    public async Task<UsageRead> ReadAsync(AccountSettings account, CancellationToken ct)
    {
        var exe = Environment.ExpandEnvironmentVariables(codexbarPath);
        if (!File.Exists(exe)) return UsageRead.Failure($"codexbar-cli not found: {exe}");

        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in new[] { "usage", "-p", account.Provider, "--json", "--no-color" })
            psi.ArgumentList.Add(arg);

        // A widget started before an installer changed PATH (for example Antigravity's agy) would not see it.
        psi.Environment["PATH"] = CurrentPath();
        foreach (var (key, value) in account.Env ?? [])
            psi.Environment[key] = Environment.ExpandEnvironmentVariables(value);

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return UsageRead.Failure($"Could not start codexbar-cli: {e.Message}");
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
            return UsageRead.Failure(ct.IsCancellationRequested ? "Cancelled" : $"Timed out after {timeout.TotalSeconds:0}s");
        }

        var output = await stdout;
        _ = await stderr;
        return string.IsNullOrWhiteSpace(output)
            ? UsageRead.Failure($"codexbar-cli exited with code {process.ExitCode} and no output")
            : UsageRead.Success(output);
    }

    static void KillTree(Process process)
    {
        try { process.Kill(entireProcessTree: true); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }

    static string CurrentPath()
    {
        var machine = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
        var user = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
        return Environment.ExpandEnvironmentVariables(machine + ";" + user);
    }
}
