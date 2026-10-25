using System.Text.Json;
using System.Text.Json.Serialization;

namespace UsageWidget.Core;

/// <summary>Hand-editable settings, stored as JSON in the user's AppData.</summary>
public sealed class WidgetSettings
{
    public int RefreshMinutes { get; set; } = 5;
    public bool ShowAnnouncements { get; set; } = true;
    public List<AccountSettings> Accounts { get; set; } = [];
    public double? Left { get; set; }
    public double? Top { get; set; }

    [JsonIgnore]
    public IEnumerable<AccountSettings> VisibleAccounts => Accounts.Where(a => !a.Hidden);

    /// <summary>The Accounts signed in on this PC, used on first run and by "Detect logins".</summary>
    /// The Gemini API lookup runs gcloud for a few seconds, so the first run skips it and only "Detect logins" does it.
    public static List<AccountSettings> DetectAccounts(bool includeGeminiApi = false) => Task.Run(async () =>
        LoginDetector.Detect(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            LoginDetector.AgyPath() is not null,
            includeGeminiApi ? await LoginDetector.GeminiApiProjectAsync(CancellationToken.None) : null)).GetAwaiter().GetResult();

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UsageWidget", "settings.json");

    /// <summary>The copy a broken settings file is kept as, since the next save replaces the original.</summary>
    public static string BrokenCopyPath(string path) => path + ".broken";

    /// <summary>
    /// Loads settings. On first run the detected Accounts are written; a broken file falls back to the
    /// detected Accounts and is first copied to <see cref="BrokenCopyPath"/>, so a hand-edit typo is never lost.
    /// </summary>
    public static WidgetSettings Load(string path, Func<List<AccountSettings>> detect)
    {
        try
        {
            if (!File.Exists(path))
            {
                var fresh = new WidgetSettings { Accounts = detect() };
                fresh.Save(path);
                return fresh;
            }
            var loaded = JsonSerializer.Deserialize<WidgetSettings>(File.ReadAllText(path), Json) ?? new WidgetSettings();
            loaded.Accounts ??= [];
            if (loaded.RefreshMinutes < 1) loaded.RefreshMinutes = 5;
            return loaded;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            try
            {
                if (File.Exists(path)) File.Copy(path, BrokenCopyPath(path), overwrite: true);
            }
            catch (Exception copy) when (copy is IOException or UnauthorizedAccessException)
            {
                // An unreadable file usually cannot be copied either; the widget still runs on the detected Accounts.
            }
            return new WidgetSettings { Accounts = detect() };
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, Json));
        File.Move(temp, path, overwrite: true);
    }
}
