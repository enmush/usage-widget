using System.Text;
using System.Windows;
using System.Windows.Media;
using UsageWidget.Core;

namespace UsageWidget;

/// <summary>Display strings and brushes for one Row; rebuilt from a <see cref="Row"/> on every refresh or clock tick.</summary>
sealed class RowView
{
    public required string Name { get; init; }
    public required string SessionText { get; init; }
    public required double SessionFraction { get; init; }
    public required Brush SessionBrush { get; init; }
    public required Visibility SessionBarVisibility { get; init; }
    public required string WeeklyText { get; init; }
    public required double WeeklyFraction { get; init; }
    public required Brush WeeklyBrush { get; init; }
    public required Visibility BarsVisibility { get; init; }
    public required string ApiText { get; init; }
    public required Visibility ApiVisibility { get; init; }
    public required string LinkText { get; init; }
    public required string LinkUrl { get; init; }
    public required string LinkTip { get; init; }
    public required Visibility LinkVisibility { get; init; }
    public required string ResetText { get; init; }
    public required string TicketText { get; init; }
    public required Visibility TicketVisibility { get; init; }
    public required double Opacity { get; init; }
    public required string Details { get; init; }
    public required Visibility StaleVisibility { get; init; }
    public required string AnnouncementText { get; init; }
    public required string AnnouncementDetails { get; init; }
    public required Visibility AnnouncementVisibility { get; init; }

    /// <param name="announcement">Shown under this Row only when it belongs to the Account the announcement is about.</param>
    public static RowView From(Row row, AnnouncedReset? announcement, DateTimeOffset now, TimeZoneInfo zone)
    {
        var hasData = row.HasData;
        return new RowView
        {
            AnnouncementText = announcement is null ? "" : Formatter.AnnouncementText(announcement, now, zone),
            AnnouncementDetails = announcement?.Text ?? "",
            AnnouncementVisibility = announcement is null ? Visibility.Collapsed : Visibility.Visible,
            Name = row.Name,
            SessionText = Percent(row.SessionPercent),
            SessionFraction = Fraction(row.SessionPercent),
            SessionBrush = BandBrush(row.SessionPercent),
            SessionBarVisibility = row.SessionPercent is null ? Visibility.Hidden : Visibility.Visible,
            WeeklyText = Percent(row.WeeklyPercent),
            WeeklyFraction = Fraction(row.WeeklyPercent),
            WeeklyBrush = BandBrush(row.WeeklyPercent),
            // An API usage Row has no Windows; its request counts take the place of both bars.
            BarsVisibility = row.Api is null ? Visibility.Visible : Visibility.Collapsed,
            ApiText = row.Api is { } api ? $"{api.Today:N0} today · {api.ThisMonth:N0} this month" : "",
            ApiVisibility = row.Api is null ? Visibility.Collapsed : Visibility.Visible,
            // No API reports a prepaid balance; AI Studio's billing page is the only place it is shown.
            LinkText = row.Api is null ? "" : "Balance",
            LinkUrl = row.Api is null ? "" : AiStudioBillingUrl,
            LinkTip = row.Api is null ? "" : "Open AI Studio billing in your browser to see the prepaid balance",
            LinkVisibility = row.Api is null ? Visibility.Collapsed : Visibility.Visible,
            ResetText = row.NearestReset is { } at ? "↻ " + Formatter.Countdown(at, now) : hasData ? "" : "—",
            TicketText = row.TicketCount > 0
                ? $"{row.TicketCount}" + (row.TicketExpiry is { } exp ? " · " + Formatter.ShortDate(exp, zone) : "")
                : "",
            TicketVisibility = row.TicketCount > 0 ? Visibility.Visible : Visibility.Collapsed,
            Opacity = row.IsStale || !hasData ? 0.55 : 1,
            StaleVisibility = row.Error is null ? Visibility.Collapsed : Visibility.Visible,
            Details = DescribeRow(row, zone),
        };
    }

    const string AiStudioBillingUrl = "https://aistudio.google.com/billing";

    static string Percent(double? p) => p is { } v ? $"{v:0}%" : "—";

    static double Fraction(double? p) => p is { } v ? Math.Clamp(v / 100, 0, 1) : 0;

    static Brush BandBrush(double? p) => (Brush)Application.Current.Resources[
        Formatter.BandFor(p ?? 0) switch
        {
            Band.Danger => "BandDanger",
            Band.Warning => "BandWarning",
            _ => "BandNormal",
        }];

    static string DescribeRow(Row row, TimeZoneInfo zone)
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
