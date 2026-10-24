namespace UsageWidget.Core;

public static class Formatter
{
    /// <summary>"32m", "2h 22m", "6d 4h"; "now" once the time has passed.</summary>
    public static string Countdown(DateTimeOffset target, DateTimeOffset now)
    {
        var left = target - now;
        if (left <= TimeSpan.Zero) return "now";
        var minutes = (int)Math.Ceiling(left.TotalMinutes);
        if (minutes < 60) return $"{minutes}m";
        if (minutes < 24 * 60) return $"{minutes / 60}h {minutes % 60}m";
        return $"{minutes / (24 * 60)}d {minutes % (24 * 60) / 60}h";
    }

    public static Band BandFor(double percent) =>
        percent >= 90 ? Band.Danger : percent >= 70 ? Band.Warning : Band.Normal;

    public static string ShortDate(DateTimeOffset time, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(time, zone);
        return $"{local.Month}/{local.Day}";
    }

    public static string LocalDateTime(DateTimeOffset time, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(time, zone).ToString("M'/'d HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// "reset announced · Oct 24, in 5h 0m". The feed keeps a reset pending until it sees it happen,
    /// so a passed time reads "due", and a reset without a time names the announcement date.
    /// </summary>
    public static string AnnouncementText(AnnouncedReset a, DateTimeOffset now, TimeZoneInfo zone)
    {
        if (a.ScheduledFor is { } at)
            return at > now
                ? $"reset announced · {MonthDay(at, zone)}, in {Countdown(at, now)}"
                : $"reset announced · due since {MonthDay(at, zone)} {TimeZoneInfo.ConvertTime(at, zone):HH:mm}";
        return a.AnnouncedAt is { } on ? $"reset announced {MonthDay(on, zone)}, time not given" : "reset announced";
    }

    static string MonthDay(DateTimeOffset time, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(time, zone).ToString("MMM d", System.Globalization.CultureInfo.InvariantCulture);
}
