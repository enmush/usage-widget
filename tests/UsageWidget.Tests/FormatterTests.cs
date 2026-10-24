using UsageWidget.Core;

namespace UsageWidget.Tests;

public class FormatterTests
{
    static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-24T14:00:00Z");

    [Theory]
    [InlineData(32, "32m")]
    [InlineData(0.5, "1m")]
    [InlineData(142, "2h 22m")]
    [InlineData(60, "1h 0m")]
    [InlineData(6 * 24 * 60 + 4 * 60 + 10, "6d 4h")]
    [InlineData(24 * 60, "1d 0h")]
    public void Countdown_uses_two_largest_units(double minutes, string expected)
    {
        Assert.Equal(expected, Formatter.Countdown(Now.AddMinutes(minutes), Now));
    }

    [Fact]
    public void Countdown_to_the_past_reads_now()
    {
        Assert.Equal("now", Formatter.Countdown(Now.AddMinutes(-5), Now));
    }

    [Theory]
    [InlineData(0, Band.Normal)]
    [InlineData(69.9, Band.Normal)]
    [InlineData(70, Band.Warning)]
    [InlineData(89.9, Band.Warning)]
    [InlineData(90, Band.Danger)]
    [InlineData(100, Band.Danger)]
    public void Percent_bands_switch_at_70_and_90(double percent, Band expected)
    {
        Assert.Equal(expected, Formatter.BandFor(percent));
    }

    [Fact]
    public void Short_date_is_month_and_day_in_the_given_zone()
    {
        var kst = TimeZoneInfo.CreateCustomTimeZone("KST", TimeSpan.FromHours(9), "KST", "KST");
        Assert.Equal("11/7", Formatter.ShortDate(DateTimeOffset.Parse("2026-11-06T23:33:32Z"), kst));
    }

    [Fact]
    public void Announcement_text_names_date_and_countdown()
    {
        var a = new AnnouncedReset(Now.AddHours(5), Now.AddHours(-1), "Resetting tonight");
        Assert.Equal("reset announced · Oct 24, in 5h 0m", Formatter.AnnouncementText(a, Now, TimeZoneInfo.Utc));
    }

    [Fact]
    public void Announcement_past_its_time_reads_due()
    {
        var a = new AnnouncedReset(Now.AddMinutes(-30), Now.AddHours(-3), "Resetting soon");
        Assert.Equal("reset announced · due since Oct 24 13:30", Formatter.AnnouncementText(a, Now, TimeZoneInfo.Utc));
    }

    [Fact]
    public void Announcement_without_a_time_names_the_announcement_date()
    {
        var a = new AnnouncedReset(null, Now.AddHours(-1), "Soon");
        Assert.Equal("reset announced Oct 24, time not given", Formatter.AnnouncementText(a, Now, TimeZoneInfo.Utc));
    }

    [Fact]
    public void Local_date_time_ignores_the_culture_date_separator()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ko-KR");
            Assert.Equal("10/24 14:00", Formatter.LocalDateTime(Now, TimeZoneInfo.Utc));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}
