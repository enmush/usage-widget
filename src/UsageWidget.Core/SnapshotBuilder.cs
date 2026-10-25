using System.Text.Json;

namespace UsageWidget.Core;

/// <summary>One Window as a Provider reports it.</summary>
public sealed record UsageWindow(double Percent, DateTimeOffset? ResetsAt);

/// <summary>
/// Turns Provider reads and the codex-resets feed into Rows. A failed read keeps the last Row as stale
/// instead of blanking it (ADR 0003).
/// </summary>
public static class SnapshotBuilder
{
    public static Row BuildRow(AccountSettings account, UsageRead read, Row? previous, DateTimeOffset now)
    {
        if (read.Row is { } fresh) return fresh with { Name = account.Name };
        var error = read.Error ?? "No usage data";
        if (previous is { HasData: true })
            return previous with { IsStale = true, Error = error };
        return Row.Empty(account.Name, error);
    }

    /// <summary>A fresh Row from parsed Windows.</summary>
    public static Row Compose(
        string name, UsageWindow? session, UsageWindow? weekly, DateTimeOffset now,
        string weeklyLabel = "Weekly", int tickets = 0, DateTimeOffset? ticketExpiry = null,
        string? plan = null, IReadOnlyList<ExtraWindow>? extras = null)
    {
        return new Row(
            name, session?.Percent, session?.ResetsAt, weekly?.Percent, weekly?.ResetsAt, weeklyLabel,
            tickets, ticketExpiry, plan, extras ?? [], now, false, null);
    }

    /// <summary>
    /// Runs a Provider parser; any surprise in its output becomes a failure instead of an exception,
    /// because these endpoints are undocumented and change without notice.
    /// </summary>
    public static UsageRead Parse(string json, Func<JsonElement, UsageRead> parse)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return parse(doc.RootElement);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or KeyNotFoundException or IndexOutOfRangeException)
        {
            return UsageRead.Failure("Unreadable usage output");
        }
    }

    public static AnnouncedReset? ParseAnnouncement(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty("scheduled_reset", out var s)
                || s.ValueKind != JsonValueKind.Object)
                return null;

            var scheduled = JsonRead.Time(s, "scheduled_for");
            var announced = JsonRead.Time(s, "announced_at");
            if (scheduled is null && announced is null) return null;
            return new AnnouncedReset(scheduled, announced, JsonRead.Str(s, "text") ?? "");
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>Lenient JSON lookups: a missing or wrongly typed value is null, never an exception.</summary>
static class JsonRead
{
    public static JsonElement? Obj(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object ? v : null;

    public static IEnumerable<JsonElement> Array(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).ToList()
            : [];

    public static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static double? Num(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    /// <summary>A number, or a number written as a string (Cloud Monitoring writes int64 values as strings).</summary>
    public static long? Long(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v)
            ? v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n
            : v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), out var s) ? s
            : null
            : null;

    public static DateTimeOffset? Time(JsonElement e, string name) =>
        Str(e, name) is { } s && DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var t) ? t : null;

    public static DateTimeOffset? UnixTime(JsonElement e, string name) =>
        Num(e, name) is { } s && s is > 0 and < 32503680000 ? DateTimeOffset.FromUnixTimeSeconds((long)s) : null;
}
