using UsageWidget.Core;

namespace UsageWidget.Tests;

/// <summary>The macOS Keychain sign-in store, through a fake runner standing in for the security tool.</summary>
public sealed class KeychainSignInStoreTests : IDisposable
{
    const string Json = """{"claudeAiOauth":{"accessToken":"a"}}""";

    readonly string home = Directory.CreateTempSubdirectory("uw-keychain-").FullName;
    readonly FakeSecurity security = new();

    public void Dispose() => Directory.Delete(home, recursive: true);

    KeychainSignInStore Store() => new(security, new FileSignInStore());

    ClaudeLogin Default => new(Path.Combine(home, ".claude"), HasOverride: false);

    [Theory]
    [InlineData(null, "Claude Code-credentials")]
    [InlineData("/Users/jun/.claude", "Claude Code-credentials-74ef0241")]
    [InlineData("/Users/jun/.claude-acct2", "Claude Code-credentials-dcd050f3")]
    public void Service_name_follows_claude_codes_rule(string? overrideFolder, string expected) =>
        Assert.Equal(expected, KeychainSignInStore.ServiceName(overrideFolder));

    [Fact]
    public void Service_name_hashes_the_nfc_form_of_the_path()
    {
        var decomposed = "/Users/jun/.claude-café";
        var composed = "/Users/jun/.claude-café";

        Assert.Equal(KeychainSignInStore.ServiceName(composed), KeychainSignInStore.ServiceName(decomposed));
    }

    [Fact]
    public async Task Default_login_is_read_from_the_unsuffixed_item()
    {
        security.Items["Claude Code-credentials"] = ("claude-code-user", Json);

        var signIn = await Store().LoadAsync(Default, CancellationToken.None);

        Assert.Equal(Json, signIn);
    }

    [Fact]
    public async Task Overridden_login_is_read_from_its_hashed_item()
    {
        var folder = Path.Combine(home, ".claude-acct2");
        security.Items[KeychainSignInStore.ServiceName(folder)] = ("claude-code-user", Json);

        var signIn = await Store().LoadAsync(new ClaudeLogin(folder, HasOverride: true), CancellationToken.None);

        Assert.Equal(Json, signIn);
    }

    [Fact]
    public async Task Without_an_item_the_credentials_file_is_used()
    {
        Directory.CreateDirectory(Default.Folder);
        File.WriteAllText(Path.Combine(Default.Folder, ".credentials.json"), Json);

        var store = Store();
        Assert.Equal(Json, await store.LoadAsync(Default, CancellationToken.None));
        Assert.True(await store.SaveAsync(Default, "{}", CancellationToken.None));
        Assert.Equal("{}", File.ReadAllText(Path.Combine(Default.Folder, ".credentials.json")));
        Assert.Empty(security.Writes);
    }

    [Fact]
    public async Task No_item_and_no_file_reads_as_not_signed_in()
    {
        Assert.Null(await Store().LoadAsync(Default, CancellationToken.None));
    }

    [Fact]
    public async Task Save_rewrites_the_same_item_with_its_account_through_stdin()
    {
        security.Items["Claude Code-credentials"] = ("jun", Json);
        var store = Store();
        await store.LoadAsync(Default, CancellationToken.None);

        Assert.True(await store.SaveAsync(Default, """{"x":"é"}""", CancellationToken.None));

        var (service, account, json) = Assert.Single(security.Writes);
        Assert.Equal("Claude Code-credentials", service);
        Assert.Equal("jun", account);
        Assert.Equal("""{"x":"é"}""", json);
        Assert.DoesNotContain(security.Arguments, args => args.Contains("""{"x":"é"}"""));
    }

    [Fact]
    public async Task Refused_write_reports_failure()
    {
        security.Items["Claude Code-credentials"] = ("claude-code-user", Json);
        security.RefuseWrites = true;
        var store = Store();
        await store.LoadAsync(Default, CancellationToken.None);

        Assert.False(await store.SaveAsync(Default, Json, CancellationToken.None));
    }

    [Fact]
    public async Task Has_sign_in_is_true_for_an_item_or_a_file()
    {
        var acct2 = new ClaudeLogin(Path.Combine(home, ".claude-acct2"), HasOverride: true);
        security.Items[KeychainSignInStore.ServiceName(acct2.Folder)] = ("claude-code-user", Json);

        Assert.True(await Store().HasSignInAsync(acct2, CancellationToken.None));
        Assert.False(await Store().HasSignInAsync(new ClaudeLogin(Path.Combine(home, ".claude-3"), true), CancellationToken.None));
    }

    /// <summary>Answers find-generic-password and records add-generic-password commands sent through stdin.</summary>
    sealed class FakeSecurity : ICommandRunner
    {
        public Dictionary<string, (string Account, string Json)> Items { get; } = [];
        public List<(string Service, string Account, string Json)> Writes { get; } = [];
        public List<string> Arguments { get; } = [];
        public bool RefuseWrites { get; set; }

        public Task<CliResult> RunAsync(string exe, IEnumerable<string> args, string? workingDirectory, CancellationToken ct, string? input = null)
        {
            var list = args.ToList();
            Arguments.Add(string.Join(" ", list));
            if (list is ["find-generic-password", "-s", var service, ..])
            {
                if (!Items.TryGetValue(service, out var item)) return Task.FromResult(new CliResult(null, "not found", 44));
                return Task.FromResult(list.Contains("-w")
                    ? new CliResult(item.Json + "\n", null, 0)
                    : new CliResult($"keychain: \"login.keychain-db\"\nattributes:\n    \"acct\"<blob>=\"{item.Account}\"\n    \"svce\"<blob>=\"{service}\"\n", null, 0));
            }
            if (list is ["-i"] && input is not null)
            {
                if (RefuseWrites) return Task.FromResult(new CliResult(null, "write refused", 1));
                var parts = System.Text.RegularExpressions.Regex.Match(input, "add-generic-password -U -a \"(.*)\" -s \"(.*)\" -X ([0-9a-f]+)");
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromHexString(parts.Groups[3].Value));
                Writes.Add((parts.Groups[2].Value, parts.Groups[1].Value, json));
                return Task.FromResult(new CliResult("", null, 0));
            }
            return Task.FromResult(new CliResult(null, "unexpected", 1));
        }
    }
}
