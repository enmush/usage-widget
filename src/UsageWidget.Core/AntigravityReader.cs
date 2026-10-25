namespace UsageWidget.Core;

/// <summary>
/// Reads Antigravity through agy's built-in <c>/usage</c> report, which spends no model tokens (ADR 0003).
/// The argument list is passed straight to agy: a shell that rewrites "/usage" as a path turns it into a model prompt.
/// </summary>
public sealed class AntigravityReader(CliRunner runner, Func<string?> findAgy, Func<DateTimeOffset> clock)
{
    public async Task<UsageRead> ReadAsync(AccountSettings account, CancellationToken ct)
    {
        if (findAgy() is not { } agy) return UsageRead.Failure("agy not found: install the Antigravity CLI");

        // agy treats its working folder as a workspace; an empty one keeps it from indexing anything.
        var folder = Directory.CreateTempSubdirectory("usage-widget-agy-").FullName;
        try
        {
            var result = await runner.RunAsync(agy, ["-p", "/usage", "--output-format", "json", "--print-timeout", "90s"], folder, ct);
            return result.Output is { } output ? Parse(account.Name, output, clock()) : UsageRead.Failure(result.Error!);
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    public static UsageRead Parse(string name, string json, DateTimeOffset now) =>
        SnapshotBuilder.Parse(json, root =>
        {
            if (JsonRead.Obj(root, "command") is not { } command || JsonRead.Obj(command, "data") is not { } data)
                return UsageRead.Failure(JsonRead.Str(root, "status") == "SUCCESS"
                    ? "agy answered without a usage report"
                    : "agy usage report failed: " + (JsonRead.Str(root, "error") ?? JsonRead.Str(root, "status") ?? "no status"));

            UsageWindow? weekly = null;
            var extras = new List<ExtraWindow>();
            foreach (var group in JsonRead.Array(data, "groups"))
            {
                foreach (var bucket in JsonRead.Array(group, "buckets"))
                {
                    if (JsonRead.Num(bucket, "remaining_fraction") is not { } remaining) continue;
                    var window = new UsageWindow(Math.Round((1 - remaining) * 100, 2), JsonRead.Time(bucket, "reset_time"));
                    if (JsonRead.Str(bucket, "id") == "gemini-weekly") weekly = window;
                    else extras.Add(new ExtraWindow(GroupTitle(JsonRead.Str(group, "name")), window.Percent, window.ResetsAt));
                }
            }
            if (weekly is null) return UsageRead.Failure("No Gemini weekly limit in agy's usage report");
            return UsageRead.Success(SnapshotBuilder.Compose(name, null, weekly, now, weeklyLabel: "Gemini Weekly", extras: extras));
        });

    /// <summary>"Claude and GPT models" → "Claude/GPT weekly".</summary>
    static string GroupTitle(string? group) =>
        (group ?? "Other").Replace(" models", "", StringComparison.OrdinalIgnoreCase).Replace(" and ", "/") + " weekly";
}
