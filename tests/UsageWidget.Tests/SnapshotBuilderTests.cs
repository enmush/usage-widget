using UsageWidget.Core;

namespace UsageWidget.Tests;

public class SnapshotBuilderTests
{
    static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-24T14:46:00Z");

    static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    static Row Build(string name, string fixture, Row? previous = null) =>
        SnapshotBuilder.BuildRow(new AccountSettings(name, "any"), UsageRead.Success(Fixture(fixture)), previous, Now);

    [Fact]
    public void Claude_row_shows_session_and_weekly_windows_with_nearest_reset()
    {
        var row = Build("Claude 1", "claude1.json");

        Assert.Equal("Claude 1", row.Name);
        Assert.Equal(14, row.SessionPercent);
        Assert.Equal(24, row.WeeklyPercent);
        Assert.Equal(DateTimeOffset.Parse("2026-10-24T19:19:59.902406Z"), row.NearestReset);
        Assert.Equal(0, row.TicketCount);
        Assert.False(row.IsStale);
        Assert.Null(row.Error);
    }

    [Fact]
    public void Second_claude_account_keeps_plan_and_scoped_weekly_window_for_hover()
    {
        var row = Build("Claude 2", "claude2.json");

        Assert.Equal(0, row.SessionPercent);
        Assert.Equal(4, row.WeeklyPercent);
        Assert.Equal("Claude Max 5x", row.Plan);
        var extra = Assert.Single(row.Extras);
        Assert.Equal("Fable only", extra.Title);
        Assert.Equal(0, extra.UsedPercent);
    }

    [Fact]
    public void Codex_row_has_no_session_and_one_reset_ticket_with_expiry()
    {
        var row = Build("Codex", "codex.json");

        Assert.Null(row.SessionPercent);
        Assert.Equal(13, row.WeeklyPercent);
        Assert.Equal(DateTimeOffset.Parse("2026-10-30T17:41:02Z"), row.NearestReset);
        Assert.Equal(1, row.TicketCount);
        Assert.Equal(DateTimeOffset.Parse("2026-11-06T23:33:32.899574Z"), row.TicketExpiry);
        Assert.Equal("ChatGPT Pro", row.Plan);
        Assert.Empty(row.Extras);
    }

    [Fact]
    public void Antigravity_row_uses_gemini_weekly_and_keeps_claude_gpt_weekly_for_hover()
    {
        var row = Build("Antigravity", "antigravity.json");

        Assert.Null(row.SessionPercent);
        Assert.Equal(0, row.WeeklyPercent);
        Assert.Equal("Gemini Weekly", row.WeeklyLabel);
        var extra = Assert.Single(row.Extras);
        Assert.Equal("Claude/GPT weekly", extra.Title);
    }

    [Fact]
    public void Error_object_without_previous_values_gives_an_empty_row_with_the_error()
    {
        var row = Build("Gemini", "gemini-error.json");

        Assert.Null(row.WeeklyPercent);
        Assert.False(row.IsStale);
        Assert.Equal("Gemini API returned 403 Forbidden", row.Error);
    }

    [Fact]
    public void Failed_read_keeps_previous_values_marked_stale()
    {
        var good = Build("Claude 1", "claude1.json");

        var row = SnapshotBuilder.BuildRow(
            new AccountSettings("Claude 1", "claude"), UsageRead.Failure("timed out"), good, Now);

        Assert.True(row.IsStale);
        Assert.Equal("timed out", row.Error);
        Assert.Equal(24, row.WeeklyPercent);
        Assert.Equal(good.UpdatedAt, row.UpdatedAt);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{\"usage\":42}")]
    [InlineData("[{\"provider\":\"x\",\"usage\":{\"primary\":{\"used_percent\":\"high\"}}}]")]
    public void Malformed_output_never_throws_and_reports_an_error(string raw)
    {
        var row = SnapshotBuilder.BuildRow(
            new AccountSettings("X", "x"), UsageRead.Success(raw), null, Now);

        Assert.NotNull(row.Error);
        Assert.Null(row.WeeklyPercent);
    }

    [Fact]
    public void Past_reset_times_are_ignored_for_nearest_reset()
    {
        var later = DateTimeOffset.Parse("2026-10-25T00:00:00Z");
        var row = SnapshotBuilder.BuildRow(
            new AccountSettings("Claude 1", "claude"), UsageRead.Success(Fixture("claude1.json")), null, later);

        Assert.Equal(DateTimeOffset.Parse("2026-10-30T19:59:59.902430Z"), row.NearestReset);
    }

    [Fact]
    public void Announcement_is_absent_when_feed_has_no_scheduled_reset()
    {
        Assert.Null(SnapshotBuilder.ParseAnnouncement(Fixture("resets-none.json")));
    }

    [Fact]
    public void Announcement_is_read_from_scheduled_reset()
    {
        const string feed = """
        {"data":{"latest_reset":null,"scheduled_reset":{"id":"1","status":"scheduled","reset_type":"regular",
        "announced_at":"2026-10-24T10:00:00Z","scheduled_for":"2026-10-24T20:00:00Z","text":"Resetting tonight",
        "source":{"type":"x_post","author":"thsottiaux","url":"https://x.com/a/status/1"}},"active_watch":null,
        "stats":{"total":1,"last_reset_at":null,"days_since_last":null,"avg_interval_days":null}},"meta":{}}
        """;

        var a = SnapshotBuilder.ParseAnnouncement(feed);

        Assert.NotNull(a);
        Assert.Equal(DateTimeOffset.Parse("2026-10-24T20:00:00Z"), a!.ScheduledFor);
        Assert.Equal("Resetting tonight", a.Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("oops")]
    [InlineData("{\"data\":{\"scheduled_reset\":\"x\"}}")]
    public void Malformed_feed_gives_no_announcement(string? feed)
    {
        Assert.Null(SnapshotBuilder.ParseAnnouncement(feed));
    }
}
