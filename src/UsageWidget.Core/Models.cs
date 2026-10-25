namespace UsageWidget.Core;

/// <summary>One Account as configured in the settings file; a Hidden account keeps its settings but has no Row.</summary>
public sealed record AccountSettings(string Name, string Provider, Dictionary<string, string>? Env = null, bool Hidden = false);

/// <summary>Result of reading one Account: a fresh Row, or the reason it could not be read.</summary>
public sealed record UsageRead(Row? Row, string? Error)
{
    public static UsageRead Success(Row row) => new(row, null);
    public static UsageRead Failure(string error) => new(null, error);
}

/// <summary>Requests a pay-as-you-go API project made today and this month, by the local calendar.</summary>
public sealed record ApiUsage(long Today, long ThisMonth);

/// <summary>A Window shown only on hover (for example Antigravity's Claude/GPT weekly).</summary>
public sealed record ExtraWindow(string Title, double UsedPercent, DateTimeOffset? ResetsAt);

/// <summary>Everything the widget shows for one Account.</summary>
public sealed record Row(
    string Name,
    double? SessionPercent,
    DateTimeOffset? SessionResetsAt,
    double? WeeklyPercent,
    DateTimeOffset? WeeklyResetsAt,
    string WeeklyLabel,
    int TicketCount,
    DateTimeOffset? TicketExpiry,
    string? Plan,
    IReadOnlyList<ExtraWindow> Extras,
    DateTimeOffset? UpdatedAt,
    bool IsStale,
    string? Error,
    ApiUsage? Api = null)
{
    public bool HasData => WeeklyPercent is not null || SessionPercent is not null || Api is not null;

    public static Row Empty(string name, string? error) =>
        new(name, null, null, null, null, "Weekly", 0, null, null, [], null, false, error);
}

/// <summary>A provider-wide reset announced ahead of time.</summary>
public sealed record AnnouncedReset(DateTimeOffset? ScheduledFor, DateTimeOffset? AnnouncedAt, string Text);

public enum Band { Normal, Warning, Danger }
