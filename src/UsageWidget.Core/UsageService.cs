namespace UsageWidget.Core;

/// <summary>One full refresh: the given Accounts plus the announced-reset feed.</summary>
public sealed class UsageService(WidgetSettings settings, HttpClient http)
{
    const string AnnouncementUrl = "https://codex-resets.com/api/v1/status";
    static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(120);

    readonly ClaudeReader claude = new(http, Home, () => DateTimeOffset.Now, IClaudeSignInStore.ForThisComputer());
    readonly CodexReader codex = new(http, Home, () => DateTimeOffset.Now);
    // Shorter than ReadTimeout, so a hung agy is reported as timed out rather than cancelled.
    readonly AntigravityReader antigravity = new(new CliRunner(ReadTimeout - TimeSpan.FromSeconds(10)), LoginDetector.AgyPath, () => DateTimeOffset.Now);
    readonly GeminiApiReader geminiApi = new(
        http, new CliRunner(TimeSpan.FromSeconds(30)), LoginDetector.GcloudPath, () => DateTimeOffset.Now, TimeZoneInfo.Local);

    static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <param name="previous">Last Rows, matched by Account name so a failed read can keep them as stale.</param>
    public async Task<(IReadOnlyList<Row> Rows, AnnouncedReset? Announcement)> RefreshAsync(
        IReadOnlyList<AccountSettings> accounts, IReadOnlyList<Row> previous, CancellationToken ct)
    {
        var announcement = settings.ShowAnnouncements ? FetchAnnouncementAsync(ct) : Task.FromResult<AnnouncedReset?>(null);
        // Each Account has its own sign-in, so all of them are read at once (ADR 0003).
        var reads = await Task.WhenAll(accounts.Select(account => ReadAsync(account, ct)));

        var now = DateTimeOffset.Now;
        var rows = accounts
            .Select((account, i) => SnapshotBuilder.BuildRow(
                account, reads[i], previous.FirstOrDefault(p => p.Name == account.Name), now))
            .ToList();
        return (rows, await announcement);
    }

    async Task<UsageRead> ReadAsync(AccountSettings account, CancellationToken ct)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(ReadTimeout);
        try
        {
            return account.Provider.ToLowerInvariant() switch
            {
                "claude" => await claude.ReadAsync(account, limit.Token),
                "codex" => await codex.ReadAsync(account, limit.Token),
                "antigravity" => await antigravity.ReadAsync(account, limit.Token),
                "gemini-api" => await geminiApi.ReadAsync(account, limit.Token),
                _ => UsageRead.Failure($"Unknown provider: {account.Provider}"),
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return UsageRead.Failure($"Timed out after {ReadTimeout.TotalSeconds:0}s");
        }
        catch (HttpRequestException e)
        {
            return UsageRead.Failure("Network error: " + e.Message);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // One Account's surprise must not fail the whole refresh and mark every Row stale.
            return UsageRead.Failure(e.Message);
        }
    }

    async Task<AnnouncedReset?> FetchAnnouncementAsync(CancellationToken ct)
    {
        try
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limit.CancelAfter(TimeSpan.FromSeconds(15));
            return SnapshotBuilder.ParseAnnouncement(await http.GetStringAsync(AnnouncementUrl, limit.Token));
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException)
        {
            return null;
        }
    }
}
