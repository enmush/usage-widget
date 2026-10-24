namespace UsageWidget.Core;

/// <summary>A Provider the widget can read, and where its own tool keeps a sign-in.</summary>
public sealed record Provider(string Id, string DisplayName, string? LoginFolderVariable, string? DefaultFolderName);

/// <summary>Builds and describes Accounts. A Login folder is stored as the Provider's own environment override.</summary>
public static class AccountCatalog
{
    public static IReadOnlyList<Provider> Providers { get; } =
    [
        new("claude", "Claude", "CLAUDE_CONFIG_DIR", ".claude"),
        new("codex", "Codex", "CODEX_HOME", ".codex"),
        new("antigravity", "Antigravity", null, null),
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
            ? Normalize(folder)
            : Path.Combine(home, defaultName);
    }

    /// <summary>A Login folder for display, with the home folder shown as "~".</summary>
    public static string DisplayFolder(string? folder, string home)
    {
        if (folder is null) return "";
        var root = home.TrimEnd(Path.DirectorySeparatorChar);
        return folder.Equals(root, StringComparison.OrdinalIgnoreCase) ? "~"
            : folder.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? "~" + folder[root.Length..]
            : folder;
    }

    public static (AccountSettings? Account, string? Error) Create(string name, string providerId, string? folder, string home)
    {
        if (string.IsNullOrWhiteSpace(name)) return (null, "Enter a name.");
        if (Find(providerId) is not { } provider) return (null, "Unknown provider.");
        if (provider.LoginFolderVariable is null || string.IsNullOrWhiteSpace(folder))
            return (new AccountSettings(name.Trim(), provider.Id), null);

        var full = Normalize(folder);
        if (!Directory.Exists(full)) return (null, "That folder does not exist.");
        if (SamePath(full, Path.Combine(home, provider.DefaultFolderName!)))
            return (new AccountSettings(name.Trim(), provider.Id), null);
        return (new AccountSettings(name.Trim(), provider.Id, new() { [provider.LoginFolderVariable] = full }), null);
    }

    /// <summary>Same Provider and same Login folder: the same sign-in, whatever the Accounts are called.</summary>
    public static bool SameLogin(AccountSettings a, AccountSettings b, string home) =>
        Is(a, b.Provider) && SamePath(LoginFolder(a, home), LoginFolder(b, home));

    public static bool SameName(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    static string Normalize(string folder) =>
        Path.GetFullPath(Environment.ExpandEnvironmentVariables(folder.Trim())).TrimEnd(Path.DirectorySeparatorChar);

    static bool SamePath(string? a, string? b) =>
        a is null || b is null ? a == b : string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);
}

/// <summary>Finds the Accounts already signed in on this PC.</summary>
public static class LoginDetector
{
    // The file each Provider's tool writes into a Login folder once signed in.
    static readonly Dictionary<string, string> SignInMarker = new() { ["claude"] = ".credentials.json", ["codex"] = "auth.json" };

    public static List<AccountSettings> Detect(string home, bool agyFound)
    {
        var found = new List<AccountSettings>();
        // Claude Code keeps working from its default folder even when the token sits elsewhere, so that one counts as is.
        AddFolders(found, home, AccountCatalog.Find("claude")!, defaultNeedsMarker: false);
        AddFolders(found, home, AccountCatalog.Find("codex")!, defaultNeedsMarker: true);
        if (agyFound) found.Add(new AccountSettings("Antigravity", "antigravity"));
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

    /// <summary>Whether Antigravity's agy CLI is installed (on PATH, in its winget package folder, or its own bin folder).</summary>
    public static bool AgyInstalled()
    {
        var path = string.Join(';',
            Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine),
            Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User));
        if (path.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Any(dir => File.Exists(Path.Combine(Environment.ExpandEnvironmentVariables(dir), "agy.exe"))))
            return true;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var packages = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
        return File.Exists(Path.Combine(localAppData, "agy", "bin", "agy.exe"))
            || (Directory.Exists(packages) && Directory.EnumerateDirectories(packages, "Google.AntigravityCLI*")
                .Any(dir => File.Exists(Path.Combine(dir, "agy.exe"))));
    }

    static void AddFolders(List<AccountSettings> found, string home, Provider provider, bool defaultNeedsMarker)
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
            if ((!isDefault || defaultNeedsMarker) && !File.Exists(Path.Combine(full, SignInMarker[provider.Id]))) continue;

            var suffix = folder[prefix.Length..].TrimStart('-', '_', '.', ' ');
            var name = suffix.Length == 0 ? provider.DisplayName : $"{provider.DisplayName} {suffix}";
            found.Add(isDefault
                ? new AccountSettings(name, provider.Id)
                : new AccountSettings(name, provider.Id, new() { [provider.LoginFolderVariable!] = full }));
        }
    }
}
