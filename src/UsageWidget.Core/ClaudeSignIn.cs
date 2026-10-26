using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace UsageWidget.Core;

/// <summary>A Claude Account's Login folder, and whether the Account names it (CLAUDE_CONFIG_DIR) or uses the default.</summary>
public sealed record ClaudeLogin(string Folder, bool HasOverride);

/// <summary>Where Claude Code keeps one Account's sign-in: the whole JSON document, read and written as a unit.</summary>
public interface IClaudeSignInStore
{
    /// <summary>The Keychain on macOS, the Login folder's credentials file elsewhere.</summary>
    static IClaudeSignInStore ForThisComputer() => OperatingSystem.IsMacOS()
        ? new KeychainSignInStore(new CliRunner(TimeSpan.FromSeconds(20)), new FileSignInStore())
        : new FileSignInStore();

    /// <summary>The sign-in JSON, or null when this Login folder has none.</summary>
    Task<string?> LoadAsync(ClaudeLogin login, CancellationToken ct);

    /// <summary>Replaces the sign-in JSON where it was loaded from; false when it cannot be written.</summary>
    Task<bool> SaveAsync(ClaudeLogin login, string json, CancellationToken ct);

    Task<bool> HasSignInAsync(ClaudeLogin login, CancellationToken ct);
}

/// <summary>The Login folder's .credentials.json, as Claude Code keeps it on Windows.</summary>
public sealed class FileSignInStore : IClaudeSignInStore
{
    static string PathOf(ClaudeLogin login) => Path.Combine(login.Folder, ".credentials.json");

    public Task<string?> LoadAsync(ClaudeLogin login, CancellationToken ct)
    {
        try
        {
            return Task.FromResult(File.Exists(PathOf(login)) ? File.ReadAllText(PathOf(login)) : null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult<string?>(null);
        }
    }

    /// <summary>Writes through a temp file, so a crash never leaves half a file.</summary>
    public Task<bool> SaveAsync(ClaudeLogin login, string json, CancellationToken ct)
    {
        var path = PathOf(login);
        var temp = path + ".usage-widget.tmp";
        try
        {
            File.WriteAllText(temp, json);
            File.Move(temp, path, overwrite: true);
            return Task.FromResult(true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(false);
        }
    }

    public Task<bool> HasSignInAsync(ClaudeLogin login, CancellationToken ct) => Task.FromResult(File.Exists(PathOf(login)));
}

/// <summary>
/// The macOS Keychain item Claude Code keeps a sign-in in, read and written through the system security tool;
/// a Login folder without an item falls back to its credentials file.
/// </summary>
public sealed partial class KeychainSignInStore(ICommandRunner security, IClaudeSignInStore fallback) : IClaudeSignInStore
{
    const string SecurityTool = "/usr/bin/security";
    const string BaseService = "Claude Code-credentials";

    // Where each Login folder's sign-in was found, so a save goes back to the same place.
    readonly ConcurrentDictionary<string, (string Service, string Account)?> found = [];

    /// <summary>
    /// Claude Code's item name: unsuffixed without CLAUDE_CONFIG_DIR, otherwise suffixed with the first 8 hex digits
    /// of the SHA-256 of the folder path in NFC form (read from Claude Code 2.1.310 on 2026-10-26).
    /// </summary>
    public static string ServiceName(string? overrideFolder) => overrideFolder is null
        ? BaseService
        : $"{BaseService}-{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(overrideFolder.Normalize(NormalizationForm.FormC))))[..8]}";

    // CLAUDE_CONFIG_DIR is hashed as typed, so a folder given with a trailing slash names another item.
    static IEnumerable<string> Services(ClaudeLogin login) => login.HasOverride
        ? [ServiceName(login.Folder), ServiceName(login.Folder + "/")]
        : [BaseService, ServiceName(login.Folder), ServiceName(login.Folder + "/")];

    public async Task<string?> LoadAsync(ClaudeLogin login, CancellationToken ct)
    {
        foreach (var service in Services(login))
        {
            var secret = await security.RunAsync(SecurityTool, ["find-generic-password", "-s", service, "-w"], null, ct);
            if (secret.Output is null) continue;
            var attributes = await security.RunAsync(SecurityTool, ["find-generic-password", "-s", service], null, ct);
            var account = attributes.Output is { } text && AccountAttribute().Match(text) is { Success: true } m ? m.Groups[1].Value : "claude-code-user";
            found[login.Folder] = (service, account);
            return secret.Output.TrimEnd('\n');
        }
        found[login.Folder] = null;
        return await fallback.LoadAsync(login, ct);
    }

    public async Task<bool> SaveAsync(ClaudeLogin login, string json, CancellationToken ct)
    {
        if (!found.TryGetValue(login.Folder, out var item) || item is not { } at) return await fallback.SaveAsync(login, json, ct);
        // Through stdin, so the tokens never appear in the process list.
        var hex = Convert.ToHexStringLower(Encoding.UTF8.GetBytes(json));
        var result = await security.RunAsync(SecurityTool, ["-i"], null, ct,
            $"add-generic-password -U -a \"{at.Account}\" -s \"{at.Service}\" -X {hex}\n");
        return result.ExitCode == 0;
    }

    public async Task<bool> HasSignInAsync(ClaudeLogin login, CancellationToken ct)
    {
        foreach (var service in Services(login))
        {
            var result = await security.RunAsync(SecurityTool, ["find-generic-password", "-s", service], null, ct);
            if (result.ExitCode == 0) return true;
        }
        return await fallback.HasSignInAsync(login, ct);
    }

    [GeneratedRegex("\"acct\"<blob>=\"([^\"]*)\"")]
    private static partial Regex AccountAttribute();
}
