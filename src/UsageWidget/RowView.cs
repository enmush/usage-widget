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
    public required string ResetText { get; init; }
    public required string TicketText { get; init; }
    public required Visibility TicketVisibility { get; init; }
    public required double Opacity { get; init; }
    public required string Details { get; init; }
    public required Visibility StaleVisibility { get; init; }

    public static RowView From(Row row, DateTimeOffset now, TimeZoneInfo zone)
    {
        var hasData = row.WeeklyPercent is not null || row.SessionPercent is not null;
        return new RowView
        {
            Name = row.Name,
            SessionText = Percent(row.SessionPercent),
            SessionFraction = Fraction(row.SessionPercent),
            SessionBrush = BandBrush(row.SessionPercent),
            SessionBarVisibility = row.SessionPercent is null ? Visibility.Hidden : Visibility.Visible,
            WeeklyText = Percent(row.WeeklyPercent),
            WeeklyFraction = Fraction(row.WeeklyPercent),
            WeeklyBrush = BandBrush(row.WeeklyPercent),
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
        var sb = new StringBuilder(row.Name);
        if (row.Plan is { } plan) sb.Append(" · ").Append(plan);
        if (row.SessionPercent is { } s)
            sb.Append($"\n5-hour: {s:0}%").Append(row.SessionResetsAt is { } sr ? $", resets {Formatter.LocalDateTime(sr, zone)}" : "");
        else if (row.WeeklyPercent is not null)
            sb.Append("\n5-hour: no active session");
        if (row.WeeklyPercent is { } w)
            sb.Append($"\n{row.WeeklyLabel}: {w:0}%").Append(row.WeeklyResetsAt is { } wr ? $", resets {Formatter.LocalDateTime(wr, zone)}" : "");
        foreach (var extra in row.Extras)
            sb.Append($"\n{extra.Title}: {extra.UsedPercent:0}%").Append(extra.ResetsAt is { } er ? $", resets {Formatter.LocalDateTime(er, zone)}" : "");
        if (row.TicketCount > 0)
            sb.Append($"\nReset tickets: {row.TicketCount}").Append(row.TicketExpiry is { } te ? $", nearest expiry {Formatter.LocalDateTime(te, zone)}" : "");
        if (row.Error is { } err)
            sb.Append(row.IsStale ? "\nStale — last read failed: " : "\nNot available: ").Append(err);
        if (row.UpdatedAt is { } u)
            sb.Append($"\nUpdated {Formatter.LocalDateTime(u, zone)}");
        return sb.ToString();
    }
}
