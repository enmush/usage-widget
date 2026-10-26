using System.Text;

namespace UsageWidget.Core;

/// <summary>What each cell of one Row shows; rebuilt from a <see cref="Row"/> on every refresh or clock tick.</summary>
public sealed record RowCells
{
    public const string AiStudioBillingUrl = "https://aistudio.google.com/billing";

    public required string Name { get; init; }
    public required string SessionText { get; init; }
    public required double SessionFraction { get; init; }
    public required Band SessionBand { get; init; }
    public required bool ShowSessionBar { get; init; }
    public required string WeeklyText { get; init; }
    public required double WeeklyFraction { get; init; }
    public required Band WeeklyBand { get; init; }
    public required bool ShowBars { get; init; }
    public required string ApiText { get; init; }
    public required bool ShowApi { get; init; }
    public required string LinkText { get; init; }
    public required string LinkUrl { get; init; }
    public required string LinkTip { get; init; }
    public required bool ShowLink { get; init; }
    public required string ResetText { get; init; }
    public required string TicketText { get; init; }
    public required bool ShowTickets { get; init; }
    public required double Opacity { get; init; }
    public required string Details { get; init; }
    public required bool ShowStaleMark { get; init; }
    public required string AnnouncementText { get; init; }
    public required string AnnouncementDetails { get; init; }
    public required bool ShowAnnouncement { get; init; }

    /// <param name="announcement">Shown under this Row only when it belongs to the Account the announcement is about.</param>
    public static RowCells From(Row row, AnnouncedReset? announcement, DateTimeOffset now, TimeZoneInfo zone)
    {
        var hasData = row.HasData;
        return new RowCells
        {
            AnnouncementText = announcement is null ? "" : Formatter.AnnouncementText(announcement, now, zone),
            AnnouncementDetails = announcement?.Text ?? "",
            ShowAnnouncement = announcement is not null,
            Name = row.Name,
            // The 5-hour cell shows the time left until its reset; the percentage stays in the bar and the tooltip.
            SessionText = row.SessionPercent is null ? "—"
                : row.SessionResetsAt is { } s && s > now ? Formatter.Countdown(s, now) : "—",
            SessionFraction = Fraction(row.SessionPercent),
            SessionBand = Formatter.BandFor(row.SessionPercent ?? 0),
            ShowSessionBar = row.SessionPercent is not null,
            WeeklyText = Percent(row.WeeklyPercent),
            WeeklyFraction = Fraction(row.WeeklyPercent),
            WeeklyBand = Formatter.BandFor(row.WeeklyPercent ?? 0),
            // An API usage Row has no Windows; its request counts take the place of both bars.
            ShowBars = row.Api is null,
            ApiText = row.Api is { } api ? $"{api.Today:N0} today · {api.ThisMonth:N0} this month" : "",
            ShowApi = row.Api is not null,
            // No API reports a prepaid balance; AI Studio's billing page is the only place it is shown.
            LinkText = row.Api is null ? "" : "Balance",
            LinkUrl = row.Api is null ? "" : AiStudioBillingUrl,
            LinkTip = row.Api is null ? "" : "Open AI Studio billing in your browser to see the prepaid balance",
            ShowLink = row.Api is not null,
            // The reset column is the weekly Window's; the 5-hour one is in its own cell.
            ResetText = row.WeeklyResetsAt is { } at && at > now ? "↻ " + Formatter.Countdown(at, now) : hasData ? "" : "—",
            TicketText = row.TicketCount > 0
                ? $"{row.TicketCount}" + (row.TicketExpiry is { } exp ? " · " + Formatter.ShortDate(exp, zone) : "")
                : "",
            ShowTickets = row.TicketCount > 0,
            Opacity = row.IsStale || !hasData ? 0.55 : 1,
            ShowStaleMark = row.Error is not null,
            Details = Describe(row, zone),
        };
    }

    static string Percent(double? p) => p is { } v ? $"{v:0}%" : "—";

    static double Fraction(double? p) => p is { } v ? Math.Clamp(v / 100, 0, 1) : 0;

    static string Describe(Row row, TimeZoneInfo zone)
    {
        string At(string label, DateTimeOffset? time) => time is { } t ? $", {label} {Formatter.LocalDateTime(t, zone)}" : "";

        var sb = new StringBuilder(row.Name);
        if (row.Plan is { } plan) sb.Append(" · ").Append(plan);
        if (row.SessionPercent is { } s)
            sb.Append($"\n5-hour: {s:0}%").Append(At("resets", row.SessionResetsAt));
        else if (row.WeeklyPercent is not null)
            sb.Append("\n5-hour: no active session");
        if (row.WeeklyPercent is { } w)
            sb.Append($"\n{row.WeeklyLabel}: {w:0}%").Append(At("resets", row.WeeklyResetsAt));
        foreach (var extra in row.Extras)
            sb.Append($"\n{extra.Title}: {extra.UsedPercent:0}%").Append(At("resets", extra.ResetsAt));
        if (row.Api is { } api)
            sb.Append($"\nAPI requests: {api.Today:N0} today, {api.ThisMonth:N0} this month");
        if (row.TicketCount > 0)
            sb.Append($"\nReset tickets: {row.TicketCount}").Append(At("nearest expiry", row.TicketExpiry));
        if (row.Error is { } err)
            sb.Append(row.IsStale ? "\nStale — last read failed: " : "\nNot available: ").Append(err);
        if (row.UpdatedAt is { } u)
            sb.Append($"\nUpdated {Formatter.LocalDateTime(u, zone)}");
        return sb.ToString();
    }
}
