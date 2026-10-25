using UsageWidget.Core;

namespace UsageWidget.Tests;

public sealed class AccountsTests : IDisposable
{
    readonly string home = Directory.CreateTempSubdirectory("uw-home-").FullName;

    public void Dispose() => Directory.Delete(home, recursive: true);

    string Folder(string name, string? file = null)
    {
        var dir = Directory.CreateDirectory(Path.Combine(home, name)).FullName;
        if (file is not null) File.WriteAllText(Path.Combine(dir, file), "{}");
        return dir;
    }

    [Fact]
    public void Default_login_folder_needs_no_override()
    {
        Folder(".claude");
        var (account, error) = AccountCatalog.Create("Claude", "claude", Path.Combine(home, ".claude"), home);

        Assert.Null(error);
        Assert.Null(account!.Env);
        Assert.Equal(Path.Combine(home, ".claude"), AccountCatalog.LoginFolder(account, home));
    }

    [Fact]
    public void Other_claude_folder_maps_to_CLAUDE_CONFIG_DIR()
    {
        var dir = Folder(".claude-acct2");
        var (account, _) = AccountCatalog.Create("Claude 2", "claude", dir, home);

        Assert.Equal(dir, account!.Env!["CLAUDE_CONFIG_DIR"]);
    }

    [Fact]
    public void Codex_folder_maps_to_CODEX_HOME()
    {
        var dir = Folder(".codex-work");
        var (account, _) = AccountCatalog.Create("Codex work", "codex", dir, home);

        Assert.Equal(dir, account!.Env!["CODEX_HOME"]);
        Assert.Equal(dir, AccountCatalog.LoginFolder(account, home));
    }

    [Fact]
    public void Antigravity_ignores_the_folder()
    {
        var (account, error) = AccountCatalog.Create("Antigravity", "antigravity", "C:\\anything", home);

        Assert.Null(error);
        Assert.Null(account!.Env);
        Assert.Null(AccountCatalog.LoginFolder(account, home));
    }

    [Theory]
    [InlineData("", "claude", "Enter a name.")]
    [InlineData("  ", "codex", "Enter a name.")]
    [InlineData("X", "dreamina", "Unknown provider.")]
    public void Invalid_input_is_rejected(string name, string provider, string message)
    {
        var (account, error) = AccountCatalog.Create(name, provider, null, home);

        Assert.Null(account);
        Assert.Equal(message, error);
    }

    [Fact]
    public void Missing_folder_is_rejected()
    {
        var (account, error) = AccountCatalog.Create("Claude 3", "claude", Path.Combine(home, "nope"), home);

        Assert.Null(account);
        Assert.Equal("That folder does not exist.", error);
    }

    [Fact]
    public void A_new_account_must_differ_from_the_list_in_name_and_sign_in()
    {
        var work = Folder(".codex-work");
        List<AccountSettings> existing = [new("Codex", "codex"), new("Work", "codex", new() { ["CODEX_HOME"] = work })];

        Assert.Equal("That name is already used.", AccountCatalog.Create("codex", "codex", "", home, existing).Error);
        Assert.Equal("That sign-in is already listed as Codex.", AccountCatalog.Create("Main", "codex", "", home, existing).Error);
        Assert.Equal("That sign-in is already listed as Work.", AccountCatalog.Create("Again", "codex", work, home, existing).Error);
        Assert.NotNull(AccountCatalog.Create("Claude", "claude", "", home, existing).Account);
    }

    [Fact]
    public void Empty_folder_means_the_default_folder()
    {
        var (account, error) = AccountCatalog.Create("Codex", "codex", "", home);

        Assert.Null(error);
        Assert.Null(account!.Env);
    }

    [Fact]
    public void Detector_finds_signed_in_folders_and_antigravity()
    {
        Folder(".claude");
        Folder(".claude-acct2", ".credentials.json");
        Folder(".claude-empty");
        Folder(".codex", "auth.json");
        Folder(".codex-old");

        var found = LoginDetector.Detect(home, agyFound: true);

        Assert.Equal(["Claude", "Claude acct2", "Codex", "Antigravity"], found.Select(a => a.Name));
        Assert.Equal(Path.Combine(home, ".claude-acct2"), found[1].Env!["CLAUDE_CONFIG_DIR"]);
    }

    [Fact]
    public void Detector_accepts_any_folder_suffix_and_case()
    {
        Folder(".claude2", ".credentials.json");
        Folder(".Codex-work", "auth.json");

        var found = LoginDetector.Detect(home, agyFound: false);

        Assert.Equal(["Claude 2", "Codex work"], found.Select(a => a.Name));
    }

    [Theory]
    [InlineData(@"C:\Users\jun\.claude", "~\\.claude")]
    [InlineData(@"C:\Users\jun2\x", @"C:\Users\jun2\x")]
    [InlineData(@"D:\logins\codex", @"D:\logins\codex")]
    public void Folders_under_home_display_with_a_tilde(string folder, string shown)
    {
        Assert.Equal(shown, AccountCatalog.DisplayFolder(folder, @"C:\Users\jun"));
    }

    [Fact]
    public void Broken_settings_file_is_kept_as_a_copy()
    {
        var path = Path.Combine(home, "settings.json");
        File.WriteAllText(path, """{"accounts":[{"name":"Mine","provider":"claude"},]""");

        var settings = WidgetSettings.Load(path, () => [new("Codex", "codex")]);
        settings.Save(path);

        Assert.Equal(["Codex"], settings.Accounts.Select(a => a.Name));
        Assert.Contains("Mine", File.ReadAllText(WidgetSettings.BrokenCopyPath(path)));
    }

    [Fact]
    public void Detector_without_logins_finds_nothing()
    {
        Assert.Empty(LoginDetector.Detect(home, agyFound: false));
    }

    [Fact]
    public void Merge_adds_only_new_logins_and_keeps_existing_names_and_hidden_flags()
    {
        Folder(".claude");
        var acct2 = Folder(".claude-acct2", ".credentials.json");
        Folder(".codex", "auth.json");
        var existing = new List<AccountSettings>
        {
            new("Claude 1", "claude"),
            new("Claude 2", "claude", new() { ["CLAUDE_CONFIG_DIR"] = acct2 }, Hidden: true),
        };

        var merged = LoginDetector.Merge(existing, LoginDetector.Detect(home, agyFound: false), home);

        Assert.Equal(["Claude 1", "Claude 2", "Codex"], merged.Select(a => a.Name));
        Assert.True(merged[1].Hidden);
    }

    [Fact]
    public void Merge_matches_folders_written_with_environment_variables()
    {
        var existing = new List<AccountSettings>
        {
            new("Claude 2", "claude", new() { ["CLAUDE_CONFIG_DIR"] = @"%USERPROFILE%\.claude-acct2" }),
        };
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var detected = new List<AccountSettings>
        {
            new("Claude acct2", "claude", new() { ["CLAUDE_CONFIG_DIR"] = Path.Combine(userProfile, ".claude-acct2") }),
        };

        Assert.Single(LoginDetector.Merge(existing, detected, userProfile));
    }

    [Fact]
    public void Merge_renames_a_new_account_whose_name_is_taken()
    {
        var codexWork = Folder(".codex-work", "auth.json");
        var existing = new List<AccountSettings> { new("Codex work", "claude") };
        var detected = new List<AccountSettings> { new("Codex work", "codex", new() { ["CODEX_HOME"] = codexWork }) };

        var merged = LoginDetector.Merge(existing, detected, home);

        Assert.Equal(["Codex work", "Codex work 2"], merged.Select(a => a.Name));
    }

    [Fact]
    public void V1_settings_without_hidden_flags_load_with_every_account_shown()
    {
        var path = Path.Combine(home, "settings.json");
        File.WriteAllText(path, """
        {"codexbarPath":"x","refreshMinutes":5,"accounts":[{"name":"Claude 1","provider":"claude"},
        {"name":"Codex","provider":"codex"}],"left":10,"top":20}
        """);

        var settings = WidgetSettings.Load(path, () => []);

        Assert.Equal(2, settings.Accounts.Count);
        Assert.All(settings.Accounts, a => Assert.False(a.Hidden));
        Assert.Equal(10, settings.Left);
    }

    [Fact]
    public void First_run_uses_detected_accounts()
    {
        var path = Path.Combine(home, "new", "settings.json");

        var settings = WidgetSettings.Load(path, () => [new("Codex", "codex")]);

        Assert.Equal(["Codex"], settings.Accounts.Select(a => a.Name));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Saved_settings_keep_hidden_flags_and_only_the_account_list()
    {
        var path = Path.Combine(home, "settings.json");
        var settings = new WidgetSettings { Accounts = [new("Codex", "codex"), new("Antigravity", "antigravity", Hidden: true)] };

        settings.Save(path);
        var loaded = WidgetSettings.Load(path, () => []);

        Assert.DoesNotContain("visibleAccounts", File.ReadAllText(path));
        Assert.Equal(["Codex"], loaded.VisibleAccounts.Select(a => a.Name));
        Assert.True(loaded.Accounts[1].Hidden);
    }

    [Fact]
    public void Gemini_api_account_keeps_its_project_and_has_no_login_folder()
    {
        var (account, error) = AccountCatalog.Create("Gemini API", "gemini-api", " jun-personal ", home);

        Assert.Null(error);
        Assert.Equal("jun-personal", AccountCatalog.Project(account!));
        Assert.Null(AccountCatalog.LoginFolder(account!, home));
    }

    [Fact]
    public void Gemini_api_account_needs_a_project()
    {
        var (account, error) = AccountCatalog.Create("Gemini API", "gemini-api", "  ", home);

        Assert.Null(account);
        Assert.Equal("Enter a Google Cloud project id.", error);
    }

    [Fact]
    public void Same_gemini_api_project_is_one_sign_in_but_another_project_is_not()
    {
        var existing = new List<AccountSettings> { AccountCatalog.Create("Gemini API", "gemini-api", "jun-personal", home).Account! };

        var (same, error) = AccountCatalog.Create("Other name", "gemini-api", "JUN-PERSONAL", home, existing);
        var (other, _) = AccountCatalog.Create("Work", "gemini-api", "work-project", home, existing);

        Assert.Null(same);
        Assert.Equal("That sign-in is already listed as Gemini API.", error);
        Assert.NotNull(other);
    }

    [Fact]
    public void Detector_adds_the_gemini_api_project_once()
    {
        var found = LoginDetector.Detect(home, agyFound: false, geminiApiProject: "jun-personal");
        var merged = LoginDetector.Merge(found, LoginDetector.Detect(home, agyFound: false, geminiApiProject: "jun-personal"), home);

        var account = Assert.Single(merged);
        Assert.Equal("Gemini API", account.Name);
        Assert.Equal("jun-personal", AccountCatalog.Project(account));
    }
}
