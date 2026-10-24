namespace UsageWidget.Core;

/// <summary>
/// The PATH as the registry holds it now. A widget started before an installer changed PATH
/// (for example Antigravity's agy) would not see the change in its own environment.
/// </summary>
public static class SystemPath
{
    public static string Current()
    {
        var machine = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
        var user = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
        return Environment.ExpandEnvironmentVariables(machine + ";" + user);
    }

    public static bool Contains(string fileName) =>
        Current().Split(';', StringSplitOptions.RemoveEmptyEntries).Any(dir => File.Exists(Path.Combine(dir, fileName)));
}
