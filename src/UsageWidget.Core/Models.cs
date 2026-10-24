namespace UsageWidget.Core;

/// <summary>One Account as configured in the settings file.</summary>
public sealed record AccountSettings(string Name, string Provider, Dictionary<string, string>? Env = null);

/// <summary>Result of one codexbar-cli run for an Account.</summary>
public sealed record UsageRead(string? Json, string? Error)
{
    public static UsageRead Success(string json) => new(json, null);
    public static UsageRead Failure(string error) => new(null, error);
}

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
    DateTimeOffset? NearestReset,
    int TicketCount,
    DateTimeOffset? TicketExpiry,
    string? Plan,
    IReadOnlyList<ExtraWindow> Extras,
    DateTimeOffset? UpdatedAt,
    bool IsStale,
    string? Error)
{
    public bool HasData => WeeklyPercent is not null || SessionPercent is not null;

    public static Row Empty(string name, string? error) =>
        new(name, null, null, null, null, "Weekly", null, 0, null, null, [], null, false, error);
}

/// <summary>A provider-wide reset announced ahead of time.</summary>
public sealed record AnnouncedReset(DateTimeOffset? ScheduledFor, DateTimeOffset? AnnouncedAt, string Text);

public enum Band { Normal, Warning, Danger }
