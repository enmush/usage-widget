using System.Diagnostics;

namespace UsageWidget.Core;

/// <summary>
/// The folders a Provider's CLI is looked up in and run with. On Windows that is the PATH as the registry holds it
/// now, since a widget started before an installer changed PATH (for example Antigravity's agy) would not see the
/// change. On macOS an app opened from Finder gets a minimal PATH, so the login shell's PATH and the usual install
/// folders are added.
/// </summary>
public static class SystemPath
{
    const string Marker = "__UW__";
    static readonly Lazy<string?> ShellPath = new(() => FromLoginShell("$PATH"));

    public static string Current() => OperatingSystem.IsWindows()
        ? Environment.ExpandEnvironmentVariables(
            (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "") + ";"
            + (Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? ""))
        : string.Join(Path.PathSeparator, MacFolders(
            Environment.GetEnvironmentVariable("PATH"), ShellPath.Value, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));

    /// <summary>The full path of the first folder holding the file, or null.</summary>
    public static string? Find(string fileName) =>
        Current().Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(dir => Path.Combine(dir.Trim(), fileName))
            .FirstOrDefault(File.Exists);

    /// <summary>The app's own PATH, then the login shell's, then where agy and the Google Cloud SDK usually install.</summary>
    public static List<string> MacFolders(string? processPath, string? shellPath, string home)
    {
        string[] usual =
        [
            $"{home}/.local/bin", "/opt/homebrew/bin", "/usr/local/bin", $"{home}/google-cloud-sdk/bin",
            "/opt/homebrew/share/google-cloud-sdk/bin", "/usr/local/share/google-cloud-sdk/bin",
        ];
        return (processPath ?? "").Split(':').Concat((shellPath ?? "").Split(':')).Concat(usual)
            .Where(dir => dir.Length > 0)
            .Distinct()
            .ToList();
    }

    /// <summary>An environment variable as the user's login shell sets it, such as a key exported in ~/.zshrc; null if unknown.</summary>
    public static string? LoginShellVariable(string name) =>
        OperatingSystem.IsWindows() ? null : FromLoginShell("$" + name) is { Length: > 0 } value ? value : null;

    /// <summary>The text a shell printed between two markers, so profile greetings around it are ignored.</summary>
    public static string? BetweenMarkers(string output)
    {
        var start = output.IndexOf(Marker, StringComparison.Ordinal);
        var end = start < 0 ? -1 : output.IndexOf(Marker, start + Marker.Length, StringComparison.Ordinal);
        return end < 0 ? null : output[(start + Marker.Length)..end];
    }

    // An interactive login shell reads both the profile and the rc file, where PATH and API keys are usually set.
    static string? FromLoginShell(string expression)
    {
        var shell = Environment.GetEnvironmentVariable("SHELL") is { Length: > 0 } s ? s : "/bin/zsh";
        try
        {
            var psi = new ProcessStartInfo(shell)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
            };
            foreach (var arg in new[] { "-i", "-l", "-c", $"printf '{Marker}%s{Marker}' \"{expression}\"" }) psi.ArgumentList.Add(arg);
            using var process = Process.Start(psi)!;
            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(TimeSpan.FromSeconds(10)))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }
            return BetweenMarkers(output.Result);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }
}
