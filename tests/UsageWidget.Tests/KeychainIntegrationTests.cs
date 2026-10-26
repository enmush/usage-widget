using System.Diagnostics;
using UsageWidget.Core;

namespace UsageWidget.Tests;

/// <summary>
/// The Keychain store against the real macOS Keychain, with an item made for a temp Login folder and removed afterwards.
/// Does nothing on other systems.
/// </summary>
public sealed class KeychainIntegrationTests : IDisposable
{
    readonly string folder = Directory.CreateTempSubdirectory("uw-claude-keychain-").FullName;
    string Service => KeychainSignInStore.ServiceName(folder);

    public void Dispose()
    {
        if (OperatingSystem.IsMacOS()) Security("delete-generic-password", "-s", Service);
        Directory.Delete(folder, recursive: true);
    }

    static (int Code, string Output) Security(params string[] args)
    {
        var psi = new ProcessStartInfo("/usr/bin/security") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output.TrimEnd('\n'));
    }

    [Fact]
    public async Task Item_is_read_and_written_back_in_place()
    {
        if (!OperatingSystem.IsMacOS()) return;
        Assert.Equal(0, Security("add-generic-password", "-a", "claude-code-user", "-s", Service, "-w", """{"claudeAiOauth":{"accessToken":"a"}}""").Code);
        var store = new KeychainSignInStore(new CliRunner(TimeSpan.FromSeconds(20)), new FileSignInStore());
        var login = new ClaudeLogin(folder, HasOverride: true);

        Assert.True(await store.HasSignInAsync(login, CancellationToken.None));
        Assert.Equal("""{"claudeAiOauth":{"accessToken":"a"}}""", await store.LoadAsync(login, CancellationToken.None));
        Assert.True(await store.SaveAsync(login, """{"claudeAiOauth":{"accessToken":"b"}}""", CancellationToken.None));

        Assert.Equal("""{"claudeAiOauth":{"accessToken":"b"}}""", Security("find-generic-password", "-s", Service, "-w").Output);
    }
}
