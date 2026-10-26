using UsageWidget.Core;

namespace UsageWidget.Tests;

public class RowCellsTests
{
    static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    static Row Subscription(double? session = 40, double? weekly = 75, int tickets = 0) => new(
        "Codex", session, session is null ? null : Now.AddMinutes(90), weekly, Now.AddDays(2).AddHours(3), "Weekly",
        tickets, tickets > 0 ? Now.AddDays(10) : null, "Plus", [], Now, IsStale: false, Error: null);

    [Fact]
    public void Five_hour_cell_shows_the_time_left_and_the_bar_its_percentage()
    {
        var cells = RowCells.From(Subscription(), null, Now, Utc);

        Assert.Equal("1h 30m", cells.SessionText);
        Assert.Equal(0.4, cells.SessionFraction, 3);
        Assert.True(cells.ShowSessionBar);
        Assert.Equal(Band.Normal, cells.SessionBand);
    }

    [Fact]
    public void No_active_session_shows_a_dash_and_no_bar()
    {
        var cells = RowCells.From(Subscription(session: null), null, Now, Utc);

        Assert.Equal("—", cells.SessionText);
        Assert.False(cells.ShowSessionBar);
    }

    [Fact]
    public void Weekly_cells_show_percentage_band_and_reset_countdown()
    {
        var cells = RowCells.From(Subscription(weekly: 92), null, Now, Utc);

        Assert.Equal("92%", cells.WeeklyText);
        Assert.Equal(Band.Danger, cells.WeeklyBand);
        Assert.Equal("↻ 2d 3h", cells.ResetText);
        Assert.True(cells.ShowBars);
        Assert.False(cells.ShowApi);
    }

    [Fact]
    public void Reset_tickets_show_count_and_nearest_expiry()
    {
        var cells = RowCells.From(Subscription(tickets: 2), null, Now, Utc);

        Assert.True(cells.ShowTickets);
        Assert.Equal("2 · 11/5", cells.TicketText);
    }

    [Fact]
    public void Api_usage_takes_the_place_of_both_bars_and_links_to_billing()
    {
        var row = Row.Empty("Gemini API", null) with { Api = new ApiUsage(1234, 56789), UpdatedAt = Now };

        var cells = RowCells.From(row, null, Now, Utc);

        Assert.False(cells.ShowBars);
        Assert.True(cells.ShowApi);
        Assert.Equal("1,234 today · 56,789 this month", cells.ApiText);
        Assert.Equal("https://aistudio.google.com/billing", cells.LinkUrl);
        Assert.True(cells.ShowLink);
    }

    [Fact]
    public void Failed_read_is_marked_and_dimmed_with_the_cause_on_hover()
    {
        var row = Subscription() with { IsStale = true, Error = "Network error: timeout" };

        var cells = RowCells.From(row, null, Now, Utc);

        Assert.True(cells.ShowStaleMark);
        Assert.Equal(0.55, cells.Opacity);
        Assert.Contains("Stale — last read failed: Network error: timeout", cells.Details);
    }

    [Fact]
    public void Row_without_data_shows_dashes()
    {
        var cells = RowCells.From(Row.Empty("Claude", null), null, Now, Utc);

        Assert.Equal("—", cells.WeeklyText);
        Assert.Equal("—", cells.ResetText);
        Assert.False(cells.ShowStaleMark);
        Assert.Equal(0.55, cells.Opacity);
    }

    [Fact]
    public void Announcement_line_appears_only_with_an_announcement()
    {
        var announcement = new AnnouncedReset(Now.AddHours(5), Now.AddDays(-1), "Global reset");

        Assert.False(RowCells.From(Subscription(), null, Now, Utc).ShowAnnouncement);
        var cells = RowCells.From(Subscription(), announcement, Now, Utc);
        Assert.True(cells.ShowAnnouncement);
        Assert.Equal("reset announced · Oct 26, in 5h 0m", cells.AnnouncementText);
        Assert.Equal("Global reset", cells.AnnouncementDetails);
    }

    [Fact]
    public void Details_list_plan_windows_and_update_time()
    {
        var cells = RowCells.From(Subscription(tickets: 1), null, Now, Utc);

        Assert.Equal(
            "Codex · Plus\n5-hour: 40%, resets 10/26 10:30\nWeekly: 75%, resets 10/28 12:00\n"
            + "Reset tickets: 1, nearest expiry 11/5 09:00\nUpdated 10/26 09:00",
            cells.Details);
    }
}
