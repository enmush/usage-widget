using System.Text.Json;
using System.Text.Json.Serialization;

namespace UsageWidget.Core;

/// <summary>Hand-editable settings, stored as JSON in the user's AppData.</summary>
public sealed class WidgetSettings
{
    public string CodexbarPath { get; set; } = @"%LOCALAPPDATA%\Programs\CodexBar\codexbar-cli.exe";
    public int RefreshMinutes { get; set; } = 5;
    public bool ShowAnnouncements { get; set; } = true;
    public List<AccountSettings> Accounts { get; set; } = DefaultAccounts();
    public double? Left { get; set; }
    public double? Top { get; set; }

    public static List<AccountSettings> DefaultAccounts() =>
    [
        new("Claude 1", "claude"),
        new("Claude 2", "claude", new() { ["CLAUDE_CONFIG_DIR"] = @"%USERPROFILE%\.claude-acct2" }),
        new("Codex", "codex"),
        new("Antigravity", "antigravity"),
    ];

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UsageWidget", "settings.json");

    /// <summary>Loads settings, writing defaults on first run; a broken file falls back to defaults without overwriting it.</summary>
    public static WidgetSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            var fresh = new WidgetSettings();
            fresh.Save(path);
            return fresh;
        }
        try
        {
            var loaded = JsonSerializer.Deserialize<WidgetSettings>(File.ReadAllText(path), Json) ?? new WidgetSettings();
            if (loaded.Accounts.Count == 0) loaded.Accounts = DefaultAccounts();
            if (loaded.RefreshMinutes < 1) loaded.RefreshMinutes = 5;
            return loaded;
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            return new WidgetSettings();
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
