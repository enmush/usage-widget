namespace UsageWidget.Core;

/// <summary>One full refresh: the given Accounts plus the announced-reset feed.</summary>
public sealed class UsageService(WidgetSettings settings, HttpClient http)
{
    const string AnnouncementUrl = "https://codex-resets.com/api/v1/status";

    readonly UsageReader reader = new(settings.CodexbarPath, TimeSpan.FromSeconds(120));

    /// <param name="previous">Last Rows, matched by Account name so a failed read can keep them as stale.</param>
    public async Task<(IReadOnlyList<Row> Rows, AnnouncedReset? Announcement)> RefreshAsync(
        IReadOnlyList<AccountSettings> accounts, IReadOnlyList<Row> previous, CancellationToken ct)
    {
        var announcement = settings.ShowAnnouncements ? FetchAnnouncementAsync(ct) : Task.FromResult<AnnouncedReset?>(null);

        // Accounts of the same provider share codexbar state files, so they run one after another;
        // different providers run in parallel (ADR 0001).
        var reads = new UsageRead[accounts.Count];
        await Task.WhenAll(Enumerable.Range(0, accounts.Count).GroupBy(i => accounts[i].Provider).Select(async group =>
        {
            foreach (var i in group)
            {
                try
                {
                    reads[i] = await reader.ReadAsync(accounts[i], ct);
                }
                catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    reads[i] = UsageRead.Failure(e.Message);
                }
            }
        }));

        var now = DateTimeOffset.Now;
        var rows = accounts
            .Select((account, i) => SnapshotBuilder.BuildRow(
                account, reads[i], previous.FirstOrDefault(p => p.Name == account.Name), now))
            .ToList();
        return (rows, await announcement);
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
