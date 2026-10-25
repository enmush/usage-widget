using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace UsageWidget.Core;

/// <summary>
/// Reads a Gemini API Account's API usage: Cloud Monitoring request counts for its Google Cloud project,
/// signed in through the Google Cloud SDK (ADR 0003).
/// </summary>
public sealed class GeminiApiReader(HttpClient http, CliRunner runner, Func<string?> findGcloud, Func<DateTimeOffset> clock, TimeZoneInfo zone)
{
    const string Service = "generativelanguage.googleapis.com";

    public async Task<UsageRead> ReadAsync(AccountSettings account, CancellationToken ct)
    {
        if (AccountCatalog.Project(account) is not { } project) return UsageRead.Failure("No Google Cloud project set for this Account");
        if (findGcloud() is not { } gcloud) return UsageRead.Failure("gcloud not found: install the Google Cloud SDK");

        var token = await runner.RunAsync(gcloud, ["auth", "print-access-token"], null, ct);
        if (token.Output?.Trim() is not { Length: > 0 } accessToken) return UsageRead.Failure("gcloud is not signed in: run gcloud auth login");

        var now = clock();
        var (midnight, monthStart) = CalendarStarts(now, zone);
        var today = await CountAsync(project, accessToken, midnight, now, ct);
        if (today.Error is not null) return UsageRead.Failure(today.Error);
        var month = await CountAsync(project, accessToken, monthStart, now, ct);
        if (month.Error is not null) return UsageRead.Failure(month.Error);

        return UsageRead.Success(Row.Empty(account.Name, null) with
        {
            Api = new ApiUsage(today.Count, month.Count),
            Plan = "Pay as you go · " + project,
            UpdatedAt = now,
        });
    }

    /// <summary>Local midnight and the local first of the month, as moments.</summary>
    public static (DateTimeOffset Midnight, DateTimeOffset MonthStart) CalendarStarts(DateTimeOffset now, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(now, zone).DateTime;
        DateTimeOffset At(DateTime day) => new(day, zone.GetUtcOffset(day));
        return (At(local.Date), At(new DateTime(local.Year, local.Month, 1)));
    }

    async Task<(long Count, string? Error)> CountAsync(string project, string token, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        // One aligned sum over the whole span; Cloud Monitoring needs a period of at least a minute.
        var seconds = Math.Max(60, (long)Math.Ceiling((end - start).TotalSeconds));
        var query = string.Join("&", new Dictionary<string, string>
        {
            ["filter"] = $"metric.type=\"serviceruntime.googleapis.com/api/request_count\" AND resource.labels.service=\"{Service}\"",
            ["interval.startTime"] = Rfc3339(end.AddSeconds(-seconds)),
            ["interval.endTime"] = Rfc3339(end),
            ["aggregation.alignmentPeriod"] = $"{seconds}s",
            ["aggregation.perSeriesAligner"] = "ALIGN_SUM",
            ["aggregation.crossSeriesReducer"] = "REDUCE_SUM",
        }.Select(p => p.Key + "=" + Uri.EscapeDataString(p.Value)));

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://monitoring.googleapis.com/v3/projects/{Uri.EscapeDataString(project)}/timeSeries?{query}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("x-goog-user-project", project);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode is HttpStatusCode.Unauthorized) return (0, "gcloud sign-in expired: run gcloud auth login");
        if (response.StatusCode is HttpStatusCode.Forbidden) return (0, "No access to Cloud Monitoring for " + project);
        if (!response.IsSuccessStatusCode) return (0, $"Cloud Monitoring request failed (HTTP {(int)response.StatusCode})");
        return ParseCount(await response.Content.ReadAsStringAsync(ct)) is { } count ? (count, null) : (0, "Unreadable Cloud Monitoring output");
    }

    static string Rfc3339(DateTimeOffset t) => t.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>The sum of every point in a timeSeries response; a response with no series means no requests.</summary>
    public static long? ParseCount(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            return JsonRead.Array(doc.RootElement, "timeSeries")
                .SelectMany(series => JsonRead.Array(series, "points"))
                .Sum(point => JsonRead.Obj(point, "value") is { } v ? JsonRead.Long(v, "int64Value") ?? 0 : 0);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
