using System.Net;
using System.Text.Json;
using UsageWidget.Core;

namespace UsageWidget.Tests;

/// <summary>Claude sign-in refresh, through a fake HTTP handler and a temp Login folder.</summary>
public sealed class ClaudeReaderTests : IDisposable
{
    static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-25T12:00:00Z");

    readonly string home = Directory.CreateTempSubdirectory("uw-claude-").FullName;
    readonly FakeHandler handler = new();
    readonly AccountSettings account = new("Claude", "claude");

    string CredentialsPath => Path.Combine(home, ".claude", ".credentials.json");

    public ClaudeReaderTests() => Directory.CreateDirectory(Path.Combine(home, ".claude"));

    public void Dispose() => Directory.Delete(home, recursive: true);

    void WriteCredentials(string access, DateTimeOffset expires) => File.WriteAllText(CredentialsPath, $$$"""
        {"claudeAiOauth":{"accessToken":"{{{access}}}","refreshToken":"refresh-1","expiresAt":{{{expires.ToUnixTimeMilliseconds()}}},
        "scopes":["user:inference"],"subscriptionType":"pro","rateLimitTier":"default_claude_ai"},"other":{"keep":true}}
        """);

    Task<UsageRead> Read() => new ClaudeReader(new HttpClient(handler), home, () => Now).ReadAsync(account, CancellationToken.None);

    JsonElement Saved() => JsonDocument.Parse(File.ReadAllText(CredentialsPath)).RootElement;

    [Fact]
    public async Task Valid_token_is_used_without_refreshing()
    {
        WriteCredentials("access-1", Now.AddHours(4));
        handler.Usage = _ => (HttpStatusCode.OK, SnapshotBuilderTests.Fixture("claude-api1.json"));

        var read = await Read();

        Assert.Equal(41, read.Row!.WeeklyPercent);
        Assert.Equal("Claude Pro", read.Row.Plan);
        Assert.Equal(0, handler.TokenCalls);
        Assert.Equal("Bearer access-1", handler.LastAuthorization);
    }

    [Fact]
    public async Task Expired_token_is_refreshed_and_written_back_keeping_other_fields()
    {
        WriteCredentials("access-1", Now.AddMinutes(-5));
        handler.Usage = auth => auth == "Bearer access-2"
            ? (HttpStatusCode.OK, SnapshotBuilderTests.Fixture("claude-api1.json"))
            : (HttpStatusCode.Unauthorized, "");

        var read = await Read();

        Assert.Null(read.Error);
        Assert.Equal(1, handler.TokenCalls);
        Assert.Contains("\"refresh_token\":\"refresh-1\"", handler.LastTokenBody);
        var oauth = Saved().GetProperty("claudeAiOauth");
        Assert.Equal("access-2", oauth.GetProperty("accessToken").GetString());
        Assert.Equal("refresh-2", oauth.GetProperty("refreshToken").GetString());
        Assert.Equal(Now.AddSeconds(28800).ToUnixTimeMilliseconds(), oauth.GetProperty("expiresAt").GetInt64());
        Assert.Equal("pro", oauth.GetProperty("subscriptionType").GetString());
        Assert.True(Saved().GetProperty("other").GetProperty("keep").GetBoolean());
        Assert.False(File.Exists(CredentialsPath + ".usage-widget.tmp"));
    }

    [Fact]
    public async Task Rejected_token_is_refreshed_once_and_retried()
    {
        WriteCredentials("access-1", Now.AddHours(4));
        handler.Usage = auth => auth == "Bearer access-2"
            ? (HttpStatusCode.OK, SnapshotBuilderTests.Fixture("claude-api1.json"))
            : (HttpStatusCode.Unauthorized, "");

        var read = await Read();

        Assert.Null(read.Error);
        Assert.Equal(1, handler.TokenCalls);
    }

    [Fact]
    public async Task Token_already_replaced_by_claude_code_is_used_without_refreshing()
    {
        WriteCredentials("access-1", Now.AddHours(4));
        // Claude Code writes a fresh token while the widget's request with the old one is in flight.
        handler.Usage = auth =>
        {
            if (auth == "Bearer access-new") return (HttpStatusCode.OK, SnapshotBuilderTests.Fixture("claude-api1.json"));
            WriteCredentials("access-new", Now.AddHours(8));
            return (HttpStatusCode.Unauthorized, "");
        };

        var read = await Read();

        Assert.Null(read.Error);
        Assert.Equal(0, handler.TokenCalls);
        Assert.Equal("access-new", Saved().GetProperty("claudeAiOauth").GetProperty("accessToken").GetString());
    }

    [Fact]
    public async Task Failed_refresh_asks_to_sign_in_again_and_leaves_the_file_alone()
    {
        WriteCredentials("access-1", Now.AddMinutes(-5));
        var before = File.ReadAllText(CredentialsPath);
        handler.Token = (HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""");

        var read = await Read();

        Assert.Null(read.Row);
        Assert.StartsWith("Sign-in expired", read.Error);
        Assert.Equal(before, File.ReadAllText(CredentialsPath));
    }

    [Fact]
    public async Task Expired_sign_in_that_cannot_be_saved_back_is_not_refreshed()
    {
        WriteCredentials("access-1", Now.AddMinutes(-5));
        var reader = new ClaudeReader(new HttpClient(handler), home, () => Now, new ReadOnlyStore());

        var read = await reader.ReadAsync(account, CancellationToken.None);

        Assert.Equal(0, handler.TokenCalls);
        Assert.Equal("Sign-in expired: open Claude Code with this Login folder", read.Error);
    }

    /// <summary>A store whose sign-in can be read but not written, like a Keychain item the widget may not change.</summary>
    sealed class ReadOnlyStore : IClaudeSignInStore
    {
        readonly FileSignInStore file = new();
        public Task<string?> LoadAsync(ClaudeLogin login, CancellationToken ct) => file.LoadAsync(login, ct);
        public Task<bool> SaveAsync(ClaudeLogin login, string json, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> HasSignInAsync(ClaudeLogin login, CancellationToken ct) => file.HasSignInAsync(login, ct);
    }

    [Fact]
    public async Task Missing_sign_in_file_says_so()
    {
        var read = await Read();

        Assert.StartsWith("Not signed in", read.Error);
        Assert.Equal(0, handler.Calls);
    }

    sealed class FakeHandler : HttpMessageHandler
    {
        public Func<string?, (HttpStatusCode, string)> Usage { get; set; } = _ => (HttpStatusCode.OK, "{}");
        public (HttpStatusCode, string) Token { get; set; } =
            (HttpStatusCode.OK, """{"access_token":"access-2","refresh_token":"refresh-2","expires_in":28800}""");
        public int Calls { get; private set; }
        public int TokenCalls { get; private set; }
        public string? LastAuthorization { get; private set; }
        public string? LastTokenBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            HttpStatusCode status;
            string body;
            if (request.RequestUri!.AbsolutePath.EndsWith("/oauth/token"))
            {
                TokenCalls++;
                LastTokenBody = await request.Content!.ReadAsStringAsync(ct);
                (status, body) = Token;
            }
            else
            {
                LastAuthorization = request.Headers.Authorization?.ToString();
                (status, body) = Usage(LastAuthorization);
            }
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }
}
