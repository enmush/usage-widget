using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace UsageWidget.Core;

/// <summary>Reads a Claude Account from Claude's OAuth usage endpoint with its Login folder's sign-in (ADR 0003).</summary>
public sealed partial class ClaudeReader(HttpClient http, string home, Func<DateTimeOffset> clock)
{
    const string UsageUrl = "https://api.anthropic.com/api/oauth/usage";
    const string TokenUrl = "https://platform.claude.com/v1/oauth/token";
    // Claude Code's public OAuth client id, the one its sign-in tokens were issued to.
    const string ClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";
    const string SignInAgain = "Sign-in expired: open Claude Code with this Login folder";

    public async Task<UsageRead> ReadAsync(AccountSettings account, CancellationToken ct)
    {
        var path = Path.Combine(AccountCatalog.LoginFolder(account, home)!, ".credentials.json");
        if (!File.Exists(path)) return UsageRead.Failure("Not signed in: open Claude Code with this Login folder");
        if (Credentials.Load(path) is not { } creds) return UsageRead.Failure("Unreadable Claude sign-in file");

        if (creds.ExpiresAt <= clock().AddMinutes(1))
        {
            if (await RefreshAsync(path, creds.AccessToken, ct) is not { } refreshed) return UsageRead.Failure(SignInAgain);
            creds = refreshed;
        }

        var (status, json) = await GetUsageAsync(creds.AccessToken, ct);
        if (status == HttpStatusCode.Unauthorized)
        {
            if (await RefreshAsync(path, creds.AccessToken, ct) is not { } refreshed) return UsageRead.Failure(SignInAgain);
            creds = refreshed;
            (status, json) = await GetUsageAsync(creds.AccessToken, ct);
        }
        if (status == HttpStatusCode.Unauthorized) return UsageRead.Failure(SignInAgain);
        if (status != HttpStatusCode.OK) return UsageRead.Failure($"Claude usage request failed (HTTP {(int)status})");

        return Parse(account.Name, json!, Plan(creds.SubscriptionType, creds.RateLimitTier), clock());
    }

    async Task<(HttpStatusCode, string?)> GetUsageAsync(string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("anthropic-beta", "oauth-2025-04-20");
        using var response = await http.SendAsync(request, ct);
        return (response.StatusCode, response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(ct) : null);
    }

    /// <summary>
    /// Refreshes the sign-in and writes the new tokens back, so Claude Code keeps working with the rotated
    /// refresh token. If Claude Code already replaced the token we just used, its fresh one is taken instead.
    /// </summary>
    async Task<Credentials?> RefreshAsync(string path, string usedToken, CancellationToken ct)
    {
        if (Credentials.Load(path) is not { } current) return null;
        if (current.AccessToken != usedToken && current.ExpiresAt > clock().AddMinutes(1)) return current;
        if (string.IsNullOrEmpty(current.RefreshToken)) return null;

        using var response = await http.PostAsJsonAsync(TokenUrl, new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = current.RefreshToken,
            ["client_id"] = ClientId,
        }, ct);
        if (!response.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var access = JsonRead.Str(doc.RootElement, "access_token");
        if (string.IsNullOrEmpty(access)) return null;
        var refresh = JsonRead.Str(doc.RootElement, "refresh_token") ?? current.RefreshToken;
        var expiresAt = clock().AddSeconds(JsonRead.Num(doc.RootElement, "expires_in") ?? 3600);

        try
        {
            Credentials.Save(path, access, refresh, expiresAt);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NullReferenceException or InvalidOperationException)
        {
            // The tokens still work for this read; Claude Code will ask to sign in again if the file stays old.
        }
        return current with { AccessToken = access, RefreshToken = refresh, ExpiresAt = expiresAt };
    }

    public static UsageRead Parse(string name, string json, string? plan, DateTimeOffset now) =>
        SnapshotBuilder.Parse(json, root =>
        {
            var session = Window(JsonRead.Obj(root, "five_hour"));
            var weekly = Window(JsonRead.Obj(root, "seven_day"));
            if (session is null && weekly is null) return UsageRead.Failure("No usage windows");

            // Model-scoped weekly limits (for example "Fable only") are shown on hover.
            var extras = JsonRead.Array(root, "limits")
                .Where(l => JsonRead.Str(l, "kind") == "weekly_scoped")
                .Select(l => (Model: JsonRead.Obj(l, "scope") is { } s && JsonRead.Obj(s, "model") is { } m ? JsonRead.Str(m, "display_name") : null, Limit: l))
                .Where(x => x.Model is not null && JsonRead.Num(x.Limit, "percent") is not null)
                .Select(x => new ExtraWindow($"{x.Model} only", JsonRead.Num(x.Limit, "percent")!.Value, JsonRead.Time(x.Limit, "resets_at")))
                .ToList();

            return UsageRead.Success(SnapshotBuilder.Compose(name, session, weekly, now, plan: plan, extras: extras));
        });

    static UsageWindow? Window(JsonElement? w) =>
        w is { } e && JsonRead.Num(e, "utilization") is { } used ? new UsageWindow(used, JsonRead.Time(e, "resets_at")) : null;

    /// <summary>"Claude Max 5x" from a rate-limit tier such as default_claude_max_5x, else the subscription type.</summary>
    public static string? Plan(string? subscriptionType, string? rateLimitTier)
    {
        if (rateLimitTier is not null && MaxTier().Match(rateLimitTier) is { Success: true } m) return $"Claude Max {m.Groups[1].Value}";
        if (string.IsNullOrWhiteSpace(subscriptionType)) return null;
        return "Claude " + char.ToUpperInvariant(subscriptionType[0]) + subscriptionType[1..];
    }

    [GeneratedRegex(@"max_(\d+x)", RegexOptions.IgnoreCase)]
    private static partial Regex MaxTier();

    sealed record Credentials(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAt, string? SubscriptionType, string? RateLimitTier)
    {
        public static Credentials? Load(string path)
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (JsonRead.Obj(doc.RootElement, "claudeAiOauth") is not { } o || JsonRead.Str(o, "accessToken") is not { } access) return null;
                var expires = JsonRead.Num(o, "expiresAt") is { } ms ? DateTimeOffset.FromUnixTimeMilliseconds((long)ms) : DateTimeOffset.MinValue;
                return new(access, JsonRead.Str(o, "refreshToken"), expires, JsonRead.Str(o, "subscriptionType"), JsonRead.Str(o, "rateLimitTier"));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                return null;
            }
        }

        /// <summary>Replaces only the token fields, through a temp file, so a crash never leaves half a file.</summary>
        public static void Save(string path, string access, string refresh, DateTimeOffset expiresAt)
        {
            var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            var oauth = root["claudeAiOauth"]!.AsObject();
            oauth["accessToken"] = access;
            oauth["refreshToken"] = refresh;
            oauth["expiresAt"] = expiresAt.ToUnixTimeMilliseconds();
            var temp = path + ".usage-widget.tmp";
            File.WriteAllText(temp, root.ToJsonString());
            File.Move(temp, path, overwrite: true);
        }
    }
}
