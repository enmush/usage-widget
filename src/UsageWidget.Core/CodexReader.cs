using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace UsageWidget.Core;

/// <summary>Reads a Codex Account from ChatGPT's usage and reset-credit endpoints with its Login folder's sign-in (ADR 0003).</summary>
public sealed class CodexReader(HttpClient http, string home, Func<DateTimeOffset> clock)
{
    const string UsageUrl = "https://chatgpt.com/backend-api/wham/usage";
    const string ResetCreditsUrl = "https://chatgpt.com/backend-api/wham/rate-limit-reset-credits";
    const string SignInAgain = "Sign-in expired: sign in to Codex again";

    public async Task<UsageRead> ReadAsync(AccountSettings account, CancellationToken ct)
    {
        var path = Path.Combine(AccountCatalog.LoginFolder(account, home)!, "auth.json");
        if (!File.Exists(path)) return UsageRead.Failure("Not signed in: sign in to Codex with this Login folder");

        string? token, accountId;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var tokens = JsonRead.Obj(doc.RootElement, "tokens");
            token = tokens is { } t ? JsonRead.Str(t, "access_token") : null;
            accountId = tokens is { } a ? JsonRead.Str(a, "account_id") : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return UsageRead.Failure("Unreadable Codex sign-in file");
        }
        if (string.IsNullOrEmpty(token)) return UsageRead.Failure("Codex is not signed in with ChatGPT");

        var (status, usage) = await GetAsync(UsageUrl, token, accountId, ct);
        if (status == HttpStatusCode.Unauthorized) return UsageRead.Failure(SignInAgain);
        if (status != HttpStatusCode.OK) return UsageRead.Failure($"Codex usage request failed (HTTP {(int)status})");

        // Ticket expiry is optional detail; the usage response still carries the count without it.
        string? credits = null;
        try
        {
            (_, credits) = await GetAsync(ResetCreditsUrl, token, accountId, ct);
        }
        catch (HttpRequestException) { }

        return Parse(account.Name, usage!, credits, clock());
    }

    async Task<(HttpStatusCode, string?)> GetAsync(string url, string token, string? accountId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (accountId is not null) request.Headers.Add("ChatGPT-Account-Id", accountId);
        using var response = await http.SendAsync(request, ct);
        return (response.StatusCode, response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(ct) : null);
    }

    /// <param name="credits">The reset-credits response, or null when it could not be read.</param>
    public static UsageRead Parse(string name, string usage, string? credits, DateTimeOffset now) =>
        SnapshotBuilder.Parse(usage, root =>
        {
            // Windows are told apart by length: Codex reports the weekly one as primary when there is no 5-hour limit.
            UsageWindow? session = null, weekly = null;
            if (JsonRead.Obj(root, "rate_limit") is { } limits)
            {
                foreach (var key in new[] { "primary_window", "secondary_window" })
                {
                    if (JsonRead.Obj(limits, key) is not { } w || JsonRead.Num(w, "used_percent") is not { } used) continue;
                    var window = new UsageWindow(used, JsonRead.UnixTime(w, "reset_at"));
                    if (JsonRead.Num(w, "limit_window_seconds") is <= 6 * 3600) session = window;
                    else weekly = window;
                }
            }
            if (session is null && weekly is null) return UsageRead.Failure("No usage windows");

            var (tickets, expiry) = Tickets(root, credits);
            var plan = JsonRead.Str(root, "plan_type") is { Length: > 0 } p ? "ChatGPT " + char.ToUpperInvariant(p[0]) + p[1..] : null;
            return UsageRead.Success(SnapshotBuilder.Compose(name, session, weekly, now, tickets: tickets, ticketExpiry: expiry, plan: plan));
        });

    static (int Count, DateTimeOffset? Expiry) Tickets(JsonElement usage, string? credits)
    {
        if (credits is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(credits);
                var available = JsonRead.Array(doc.RootElement, "credits").Where(c => JsonRead.Str(c, "status") == "available").ToList();
                return (available.Count, available.Select(c => JsonRead.Time(c, "expires_at")).Where(t => t is not null).Min());
            }
            catch (JsonException) { }
        }
        var count = JsonRead.Obj(usage, "rate_limit_reset_credits") is { } r ? JsonRead.Num(r, "available_count") : null;
        return (count is > 0 and < 1000 ? (int)count : 0, null);
    }
}
