namespace UsageWidget.Core;

/// <summary>
/// A Provider the widget can read, and where its own tool keeps a sign-in; an API Provider names a cloud project instead.
/// </summary>
public sealed record Provider(
    string Id, string DisplayName, string? LoginFolderVariable, string? DefaultFolderName, string? ProjectVariable = null);

/// <summary>Builds and describes Accounts. A Login folder is stored as the Provider's own environment override.</summary>
public static class AccountCatalog
{
    public static IReadOnlyList<Provider> Providers { get; } =
    [
        new("claude", "Claude", "CLAUDE_CONFIG_DIR", ".claude"),
        new("codex", "Codex", "CODEX_HOME", ".codex"),
        new("antigravity", "Antigravity", null, null),
        new("gemini-api", "Gemini API", null, null, "GOOGLE_CLOUD_PROJECT"),
    ];

    public static Provider? Find(string id) =>
        Providers.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    public static bool Is(AccountSettings account, string id) =>
        string.Equals(account.Provider, id, StringComparison.OrdinalIgnoreCase);

    /// <summary>The Account's Login folder (its override or the default one), or null for Providers without one.</summary>
    public static string? LoginFolder(AccountSettings account, string home)
    {
        if (Find(account.Provider) is not { LoginFolderVariable: { } variable, DefaultFolderName: { } defaultName }) return null;
        return account.Env is not null && account.Env.TryGetValue(variable, out var folder) && !string.IsNullOrWhiteSpace(folder)
            ? Normalize(folder, home)
            : Path.Combine(home, defaultName);
    }

    /// <summary>The Google Cloud project of an API Provider's Account, or null.</summary>
    public static string? Project(AccountSettings account) =>
        Find(account.Provider) is { ProjectVariable: { } variable } && account.Env is not null
        && account.Env.TryGetValue(variable, out var project) && !string.IsNullOrWhiteSpace(project)
            ? project.Trim()
            : null;

    /// <summary>A Login folder for display, with the home folder shown as "~".</summary>
    public static string DisplayFolder(string? folder, string home)
    {
        if (folder is null) return "";
        var root = home.TrimEnd(Path.DirectorySeparatorChar);
        return folder.Equals(root, StringComparison.OrdinalIgnoreCase) ? "~"
            : folder.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? "~" + folder[root.Length..]
            : folder;
    }

    /// <param name="existing">Accounts already listed; the new one must differ in name and in sign-in.</param>
    public static (AccountSettings? Account, string? Error) Create(
        string name, string providerId, string? folder, string home, IReadOnlyList<AccountSettings>? existing = null)
    {
        var (account, error) = Build(name, providerId, folder, home);
        if (account is null || existing is null) return (account, error);
        if (existing.Any(a => SameName(a.Name, account.Name))) return (null, "That name is already used.");
        if (existing.FirstOrDefault(a => SameLogin(a, account, home)) is { } same)
            return (null, $"That sign-in is already listed as {same.Name}.");
        return (account, null);
    }

    static (AccountSettings? Account, string? Error) Build(string name, string providerId, string? folder, string home)
    {
        if (string.IsNullOrWhiteSpace(name)) return (null, "Enter a name.");
        if (Find(providerId) is not { } provider) return (null, "Unknown provider.");
        if (provider.ProjectVariable is { } projectVariable)
            return string.IsNullOrWhiteSpace(folder)
                ? (null, "Enter a Google Cloud project id.")
                : (new AccountSettings(name.Trim(), provider.Id, new() { [projectVariable] = folder.Trim() }), null);
        if (provider.LoginFolderVariable is null || string.IsNullOrWhiteSpace(folder))
            return (new AccountSettings(name.Trim(), provider.Id), null);

        var full = Normalize(folder, home);
        if (!Directory.Exists(full)) return (null, "That folder does not exist.");
        if (SamePath(full, Path.Combine(home, provider.DefaultFolderName!)))
            return (new AccountSettings(name.Trim(), provider.Id), null);
        return (new AccountSettings(name.Trim(), provider.Id, new() { [provider.LoginFolderVariable] = full }), null);
    }

    /// <summary>Same Provider and same Login folder: the same sign-in, whatever the Accounts are called.</summary>
    public static bool SameLogin(AccountSettings a, AccountSettings b, string home) =>
        Is(a, b.Provider) && SamePath(LoginFolder(a, home), LoginFolder(b, home))
        && string.Equals(Project(a), Project(b), StringComparison.OrdinalIgnoreCase);

    public static bool SameName(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>A full path without a trailing separator; "~" at the start stands for the home folder.</summary>
    static string Normalize(string folder, string? home = null)
    {
        var text = Environment.ExpandEnvironmentVariables(folder.Trim());
        if (text == "~" || text.StartsWith("~/") || text.StartsWith("~\\"))
            text = Path.Join(home ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), text[1..].TrimStart('/', '\\'));
        return Path.GetFullPath(text).TrimEnd(Path.DirectorySeparatorChar);
    }

    static bool SamePath(string? a, string? b) =>
        a is null || b is null ? a == b : string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);
}

/// <summary>Finds the Accounts already signed in on this PC.</summary>
public static class LoginDetector
{
    // The file each Provider's tool writes into a Login folder once signed in.
    static readonly Dictionary<string, string> SignInMarker = new() { ["claude"] = ".credentials.json", ["codex"] = "auth.json" };

    /// <param name="geminiApiProject">The Google Cloud project of this PC's Gemini API key, if one was found.</param>
    /// <param name="claudeSignedIn">Whether a Claude Login folder has a sign-in where its file is not (the macOS Keychain).</param>
    public static List<AccountSettings> Detect(
        string home, bool agyFound, string? geminiApiProject = null, Func<ClaudeLogin, bool>? claudeSignedIn = null)
    {
        var found = new List<AccountSettings>();
        // Claude Code keeps working from its default folder even when the token sits elsewhere, so that one counts as is.
        AddFolders(found, home, AccountCatalog.Find("claude")!, defaultNeedsMarker: false, claudeSignedIn);
        AddFolders(found, home, AccountCatalog.Find("codex")!, defaultNeedsMarker: true);
        if (agyFound) found.Add(new AccountSettings("Antigravity", "antigravity"));
        if (geminiApiProject is not null)
            found.Add(AccountCatalog.Create("Gemini API", "gemini-api", geminiApiProject, home).Account!);
        return found;
    }

    /// <summary>Appends detected Accounts that are not already listed; existing Accounts are never changed or removed.</summary>
    public static List<AccountSettings> Merge(IReadOnlyList<AccountSettings> existing, IEnumerable<AccountSettings> detected, string home)
    {
        var merged = existing.ToList();
        foreach (var account in detected)
        {
            if (merged.Any(m => AccountCatalog.SameLogin(m, account, home))) continue;
            var name = account.Name;
            for (var n = 2; merged.Any(m => AccountCatalog.SameName(m.Name, name)); n++) name = $"{account.Name} {n}";
            merged.Add(account with { Name = name });
        }
        return merged;
    }

    /// <summary>Antigravity's agy CLI (on PATH, in its own bin folder, or in its winget package folder), or null.</summary>
    public static string? AgyPath()
    {
        var onPath = SystemPath.Find(OperatingSystem.IsWindows() ? "agy.exe" : "agy");
        if (onPath is not null || !OperatingSystem.IsWindows()) return onPath;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var own = Path.Combine(localAppData, "agy", "bin", "agy.exe");
        if (File.Exists(own)) return own;
        var packages = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
        return Directory.Exists(packages)
            ? Directory.EnumerateDirectories(packages, "Google.AntigravityCLI*").Select(dir => Path.Combine(dir, "agy.exe")).FirstOrDefault(File.Exists)
            : null;
    }

    /// <summary>The Google Cloud SDK's gcloud, or null.</summary>
    public static string? GcloudPath() => SystemPath.Find(OperatingSystem.IsWindows() ? "gcloud.cmd" : "gcloud");

    /// <summary>
    /// The project that owns the GEMINI_API_KEY set on this PC, looked up through gcloud; null when there is
    /// no key, no gcloud or no access.
    /// </summary>
    public static async Task<string?> GeminiApiProjectAsync(CancellationToken ct)
    {
        // A Mac app opened from Finder does not see keys exported in the shell profile, so the login shell is asked too.
        var key = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? SystemPath.LoginShellVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(key) || GcloudPath() is not { } gcloud) return null;

        var runner = new CliRunner(TimeSpan.FromSeconds(30));
        var parent = await runner.RunAsync(gcloud, ["services", "api-keys", "lookup", key, "--format=value(parent)"], null, ct);
        // "projects/<number>/locations/global"
        if (parent.Output?.Trim().Split('/') is not ["projects", var number, ..]) return null;
        var id = await runner.RunAsync(gcloud, ["projects", "describe", number, "--format=value(projectId)"], null, ct);
        return id.Output?.Trim() is { Length: > 0 } projectId ? projectId : number;
    }

    static void AddFolders(
        List<AccountSettings> found, string home, Provider provider, bool defaultNeedsMarker, Func<ClaudeLogin, bool>? signedIn = null)
    {
        if (!Directory.Exists(home)) return;
        var prefix = provider.DefaultFolderName!;
        var folders = Directory.EnumerateDirectories(home, prefix + "*")
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name.Length == prefix.Length ? "" : name, StringComparer.OrdinalIgnoreCase);

        foreach (var folder in folders)
        {
            var full = Path.Combine(home, folder);
            var isDefault = folder.Length == prefix.Length;
            if ((!isDefault || defaultNeedsMarker) && !File.Exists(Path.Combine(full, SignInMarker[provider.Id]))
                && signedIn?.Invoke(new ClaudeLogin(full, HasOverride: !isDefault)) != true)
                continue;

            var suffix = folder[prefix.Length..].TrimStart('-', '_', '.', ' ');
            var name = suffix.Length == 0 ? provider.DisplayName : $"{provider.DisplayName} {suffix}";
            found.Add(isDefault
                ? new AccountSettings(name, provider.Id)
                : new AccountSettings(name, provider.Id, new() { [provider.LoginFolderVariable!] = full }));
        }
    }
}
