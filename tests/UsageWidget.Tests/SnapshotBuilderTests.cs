using UsageWidget.Core;

namespace UsageWidget.Tests;

public class SnapshotBuilderTests
{
    static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-25T12:00:00Z");

    internal static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    static Row Fresh(UsageRead read)
    {
        Assert.Null(read.Error);
        return read.Row!;
    }

    [Fact]
    public void Claude_row_shows_session_and_weekly_windows_with_nearest_reset()
    {
        var row = Fresh(ClaudeReader.Parse("Claude 1", Fixture("claude-api1.json"), "Claude Pro", Now));

        Assert.Equal("Claude 1", row.Name);
        Assert.Equal(19, row.SessionPercent);
        Assert.Equal(41, row.WeeklyPercent);
        Assert.Equal(DateTimeOffset.Parse("2026-10-25T15:50:00.416045Z"), row.NearestReset);
        Assert.Equal("Claude Pro", row.Plan);
        Assert.Empty(row.Extras);
        Assert.Equal(0, row.TicketCount);
        Assert.False(row.IsStale);
    }

    [Fact]
    public void Claude_model_scoped_weekly_limit_is_kept_for_hover()
    {
        var row = Fresh(ClaudeReader.Parse("Claude 2", Fixture("claude-api2.json"), null, Now));

        Assert.Equal(2, row.SessionPercent);
        Assert.Equal(5, row.WeeklyPercent);
        var extra = Assert.Single(row.Extras);
        Assert.Equal("Fable only", extra.Title);
        Assert.Equal(0, extra.UsedPercent);
    }

    [Theory]
    [InlineData("team", "default_claude_max_5x", "Claude Max 5x")]
    [InlineData("max", "default_claude_max_20x", "Claude Max 20x")]
    [InlineData("pro", "default_claude_ai", "Claude Pro")]
    [InlineData(null, null, null)]
    public void Claude_plan_comes_from_the_sign_in(string? subscription, string? tier, string? plan)
    {
        Assert.Equal(plan, ClaudeReader.Plan(subscription, tier));
    }

    [Fact]
    public void Codex_weekly_window_is_found_by_length_with_tickets_and_expiry()
    {
        var row = Fresh(CodexReader.Parse("Codex", Fixture("codex-usage.json"), Fixture("codex-reset-credits.json"), Now));

        Assert.Null(row.SessionPercent);
        Assert.Equal(26, row.WeeklyPercent);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1793513474), row.WeeklyResetsAt);
        Assert.Equal(1, row.TicketCount);
        Assert.Equal(DateTimeOffset.Parse("2026-11-06T23:33:32.899574Z"), row.TicketExpiry);
        Assert.Equal("ChatGPT Pro", row.Plan);
    }

    [Fact]
    public void Codex_five_hour_window_becomes_the_session()
    {
        const string usage = """
        {"plan_type":"plus","rate_limit":{
          "primary_window":{"used_percent":40,"limit_window_seconds":18000,"reset_at":1793400000},
          "secondary_window":{"used_percent":10,"limit_window_seconds":604800,"reset_at":1793900000}}}
        """;

        var row = Fresh(CodexReader.Parse("Codex", usage, null, Now));

        Assert.Equal(40, row.SessionPercent);
        Assert.Equal(10, row.WeeklyPercent);
        Assert.Equal("ChatGPT Plus", row.Plan);
    }

    [Fact]
    public void Codex_ticket_count_falls_back_to_the_usage_response_without_expiry()
    {
        var row = Fresh(CodexReader.Parse("Codex", Fixture("codex-usage.json"), null, Now));

        Assert.Equal(1, row.TicketCount);
        Assert.Null(row.TicketExpiry);
    }

    [Fact]
    public void Codex_spent_tickets_are_not_counted()
    {
        const string credits = """
        {"credits":[{"status":"redeemed","expires_at":"2026-10-26T00:00:00Z"},
                    {"status":"available","expires_at":"2026-11-20T00:00:00Z"},
                    {"status":"available","expires_at":"2026-11-10T00:00:00Z"}]}
        """;

        var row = Fresh(CodexReader.Parse("Codex", Fixture("codex-usage.json"), credits, Now));

        Assert.Equal(2, row.TicketCount);
        Assert.Equal(DateTimeOffset.Parse("2026-11-10T00:00:00Z"), row.TicketExpiry);
    }

    [Fact]
    public void Antigravity_row_uses_gemini_weekly_and_keeps_claude_gpt_weekly_for_hover()
    {
        var row = Fresh(AntigravityReader.Parse("Antigravity", Fixture("agy-usage.json"), Now));

        Assert.Null(row.SessionPercent);
        Assert.Equal(0.54, row.WeeklyPercent);
        Assert.Equal("Gemini Weekly", row.WeeklyLabel);
        var extra = Assert.Single(row.Extras);
        Assert.Equal("Claude/GPT weekly", extra.Title);
        Assert.Equal(0, extra.UsedPercent);
    }

    [Fact]
    public void Antigravity_answer_without_a_usage_report_is_an_error()
    {
        // What agy prints when "/usage" reaches the model as a prompt instead of running the slash command.
        const string prompted = """{"status":"SUCCESS","response":"","denied_actions":[{"action":"command"}]}""";

        var read = AntigravityReader.Parse("Antigravity", prompted, Now);

        Assert.Null(read.Row);
        Assert.Equal("agy answered without a usage report", read.Error);
    }

    [Fact]
    public void Monitoring_counts_sum_every_point_and_no_series_means_zero()
    {
        const string series = """
        {"timeSeries":[{"points":[{"value":{"int64Value":"3"}},{"value":{"int64Value":"4"}}]},
                       {"points":[{"value":{"int64Value":"5"}}]}],"unit":"1"}
        """;

        Assert.Equal(12, GeminiApiReader.ParseCount(series));
        Assert.Equal(0, GeminiApiReader.ParseCount("""{"unit":"1"}"""));
        Assert.Null(GeminiApiReader.ParseCount("not json"));
    }

    [Fact]
    public void Calendar_starts_follow_the_local_zone()
    {
        var seoul = TimeZoneInfo.CreateCustomTimeZone("KST", TimeSpan.FromHours(9), "KST", "KST");
        // 2026-11-01 01:30 in Seoul is still October in UTC.
        var (midnight, month) = GeminiApiReader.CalendarStarts(DateTimeOffset.Parse("2026-10-31T16:30:00Z"), seoul);

        Assert.Equal(DateTimeOffset.Parse("2026-11-01T00:00:00+09:00"), midnight);
        Assert.Equal(DateTimeOffset.Parse("2026-11-01T00:00:00+09:00"), month);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{\"five_hour\":42}")]
    [InlineData("{\"five_hour\":{\"utilization\":\"high\"}}")]
    public void Malformed_claude_output_never_throws_and_reports_an_error(string raw)
    {
        var read = ClaudeReader.Parse("X", raw, null, Now);

        Assert.Null(read.Row);
        Assert.NotNull(read.Error);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"rate_limit\":[]}")]
    [InlineData("{\"rate_limit\":{\"primary_window\":{\"used_percent\":\"x\"}}}")]
    public void Malformed_codex_output_never_throws_and_reports_an_error(string raw)
    {
        Assert.NotNull(CodexReader.Parse("X", raw, "also not json", Now).Error);
    }

    [Fact]
    public void Error_without_previous_values_gives_an_empty_row_with_the_error()
    {
        var row = SnapshotBuilder.BuildRow(new AccountSettings("Codex", "codex"), UsageRead.Failure("Sign-in expired"), null, Now);

        Assert.Null(row.WeeklyPercent);
        Assert.False(row.IsStale);
        Assert.Equal("Sign-in expired", row.Error);
    }

    [Fact]
    public void Failed_read_keeps_previous_values_marked_stale()
    {
        var good = Fresh(ClaudeReader.Parse("Claude 1", Fixture("claude-api1.json"), null, Now));

        var row = SnapshotBuilder.BuildRow(new AccountSettings("Claude 1", "claude"), UsageRead.Failure("timed out"), good, Now);

        Assert.True(row.IsStale);
        Assert.Equal("timed out", row.Error);
        Assert.Equal(41, row.WeeklyPercent);
        Assert.Equal(good.UpdatedAt, row.UpdatedAt);
    }

    [Fact]
    public void Failed_read_keeps_previous_api_usage_marked_stale()
    {
        var good = Row.Empty("Gemini API", null) with { Api = new ApiUsage(2, 30) };

        var row = SnapshotBuilder.BuildRow(new AccountSettings("Gemini API", "gemini-api"), UsageRead.Failure("offline"), good, Now);

        Assert.True(row.IsStale);
        Assert.Equal(new ApiUsage(2, 30), row.Api);
    }

    [Fact]
    public void Past_reset_times_are_ignored_for_nearest_reset()
    {
        var later = DateTimeOffset.Parse("2026-10-26T00:00:00Z");

        var row = Fresh(ClaudeReader.Parse("Claude 1", Fixture("claude-api1.json"), null, later));

        Assert.Equal(DateTimeOffset.Parse("2026-10-30T20:00:00.416112Z"), row.NearestReset);
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

    [Fact]
    public void Announcement_with_a_time_but_no_announcement_date_is_kept()
    {
        const string feed = """{"data":{"scheduled_reset":{"scheduled_for":"2026-10-25T01:00:00Z","text":"Tomorrow"}}}""";

        var a = SnapshotBuilder.ParseAnnouncement(feed);

        Assert.NotNull(a);
        Assert.Null(a!.AnnouncedAt);
        Assert.Equal(DateTimeOffset.Parse("2026-10-25T01:00:00Z"), a.ScheduledFor);
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
