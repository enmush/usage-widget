using UsageWidget.Core;

namespace UsageWidget.Tests;

public sealed class CodexReaderTests : IDisposable
{
    readonly string home = Directory.CreateTempSubdirectory("usage-widget-codex-").FullName;

    public void Dispose() => Directory.Delete(home, recursive: true);

    Task<UsageRead> Read() =>
        new CodexReader(new HttpClient(), home, () => DateTimeOffset.Now).ReadAsync(new AccountSettings("Codex", "codex"), CancellationToken.None);

    [Theory]
    [InlineData("keyring")]
    [InlineData("auto")]
    public async Task Sign_in_kept_in_the_credential_store_is_named_as_the_cause(string store)
    {
        var folder = Directory.CreateDirectory(Path.Combine(home, ".codex")).FullName;
        File.WriteAllText(Path.Combine(folder, "config.toml"), $"model = \"gpt-5\"\ncli_auth_credentials_store = \"{store}\"\n");

        var read = await Read();

        Assert.Equal("Codex keeps this sign-in in the system credential store, which the widget cannot read yet", read.Error);
    }

    [Fact]
    public async Task Missing_sign_in_file_without_a_credential_store_reads_not_signed_in()
    {
        Directory.CreateDirectory(Path.Combine(home, ".codex"));

        var read = await Read();

        Assert.Equal("Not signed in: sign in to Codex with this Login folder", read.Error);
    }
}
