using System.Text.Json;
using System.Text.RegularExpressions;

namespace UsageWidget.Core;

/// <summary>
/// Turns codexbar-cli JSON and the codex-resets feed into Rows. Parsing is lenient:
/// unexpected shapes become an error on the Row instead of an exception.
/// </summary>
public static partial class SnapshotBuilder
{
    const string ResetTicketWindowId = "reset-credits";

    public static Row BuildRow(AccountSettings account, UsageRead read, Row? previous, DateTimeOffset now)
    {
        var error = read.Error;
        Row? fresh = null;
        if (error is null)
        {
            try
            {
                fresh = ParseRow(account.Name, read.Json ?? "", now, out error);
            }
            catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
            {
                error = "Unreadable usage output";
            }
        }

        if (fresh is not null) return fresh;
        if (previous is { WeeklyPercent: not null } or { SessionPercent: not null })
            return previous with { IsStale = true, Error = error };
        return Row.Empty(account.Name, error);
    }

    static Row? ParseRow(string name, string json, DateTimeOffset now, out string? error)
    {
        error = null;
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
        {
            error = "No usage data";
            return null;
        }

        var entry = doc.RootElement[0];
        if (entry.ValueKind != JsonValueKind.Object)
        {
            error = "No usage data";
            return null;
        }
        if (Str(entry, "error") is { } reported)
        {
            error = reported;
            return null;
        }
        if (!entry.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
        {
            error = "No usage data";
            return null;
        }

        var session = Window(usage, "primary");
        var weekly = Window(usage, "secondary");
        if (weekly is null && session is null)
        {
            error = "No usage windows";
            return null;
        }

        int tickets = 0;
        DateTimeOffset? ticketExpiry = null;
        var extras = new List<ExtraWindow>();
        if (usage.TryGetProperty("extra_rate_windows", out var extraList) && extraList.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in extraList.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var w = item.TryGetProperty("window", out var we) && we.ValueKind == JsonValueKind.Object ? we : default;
                if (w.ValueKind != JsonValueKind.Object) continue;
                if (Str(item, "id") == ResetTicketWindowId)
                {
                    tickets = TicketCount(Str(w, "reset_description"));
                    ticketExpiry = Time(w, "resets_at");
                    continue;
                }
                extras.Add(new ExtraWindow(Str(item, "title") ?? Str(item, "id") ?? "Extra", Num(w, "used_percent") ?? 0, Time(w, "resets_at")));
            }
        }

        var sessionPercent = session is { Informational: false } ? session.Percent : null;
        var weeklyPercent = weekly is { Informational: false } ? weekly.Percent : null;
        var nearest = new[] { session, weekly }
            .Where(w => w is { Informational: false, ResetsAt: not null } && w.ResetsAt > now)
            .Select(w => w!.ResetsAt)
            .Min();

        return new Row(
            name,
            sessionPercent,
            session?.Informational == false ? session.ResetsAt : null,
            weeklyPercent,
            weekly?.ResetsAt,
            Str(usage, "secondary_label") ?? "Weekly",
            nearest,
            tickets,
            ticketExpiry,
            Str(usage, "login_method"),
            extras,
            Time(usage, "updated_at") ?? now,
            false,
            null);
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

            var announced = Time(s, "announced_at");
            if (announced is null) return null;
            return new AnnouncedReset(Time(s, "scheduled_for"), announced.Value, Str(s, "text") ?? "", Str(s, "reset_type") ?? "regular");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    sealed record WindowValue(double? Percent, DateTimeOffset? ResetsAt, bool Informational);

    static WindowValue? Window(JsonElement usage, string name)
    {
        if (!usage.TryGetProperty(name, out var w) || w.ValueKind != JsonValueKind.Object) return null;
        var percent = Num(w, "used_percent");
        if (percent is null) return null;
        var informational = w.TryGetProperty("is_informational", out var i) && i.ValueKind == JsonValueKind.True;
        return new WindowValue(percent, Time(w, "resets_at"), informational);
    }

    static int TicketCount(string? description)
    {
        if (description is null) return 1;
        var m = LeadingNumber().Match(description);
        return m.Success ? int.Parse(m.Value) : 1;
    }

    static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static double? Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    static DateTimeOffset? Time(JsonElement e, string name) =>
        Str(e, name) is { } s && DateTimeOffset.TryParse(s, out var t) ? t : null;

    [GeneratedRegex(@"\d+")]
    private static partial Regex LeadingNumber();
}
