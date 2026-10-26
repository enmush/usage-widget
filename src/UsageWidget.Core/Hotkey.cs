namespace UsageWidget.Core;

/// <summary>Modifier keys of a <see cref="Hotkey"/>, with the values Win32 RegisterHotKey expects.</summary>
[Flags]
public enum HotkeyModifiers { None = 0, Alt = 1, Ctrl = 2, Shift = 4, Win = 8 }

/// <summary>
/// The global shortcut that shows or hides the widget, stored in settings as text such as "Ctrl+F1".
/// The key is a Win32 virtual-key code.
/// </summary>
public sealed record Hotkey(HotkeyModifiers Modifiers, int Key)
{
    static readonly Dictionary<string, int> NamedKeys = BuildNamedKeys();

    static readonly Dictionary<string, HotkeyModifiers> ModifierNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ctrl"] = HotkeyModifiers.Ctrl, ["Control"] = HotkeyModifiers.Ctrl, ["Alt"] = HotkeyModifiers.Alt,
        ["Shift"] = HotkeyModifiers.Shift, ["Win"] = HotkeyModifiers.Win,
    };

    static Dictionary<string, int> BuildNamedKeys()
    {
        var keys = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Space"] = 0x20, ["PageUp"] = 0x21, ["PageDown"] = 0x22, ["End"] = 0x23, ["Home"] = 0x24,
            ["Left"] = 0x25, ["Up"] = 0x26, ["Right"] = 0x27, ["Down"] = 0x28, ["Insert"] = 0x2D, ["Delete"] = 0x2E,
            ["Pause"] = 0x13, ["`"] = 0xC0,
        };
        for (var i = 1; i <= 24; i++) keys[$"F{i}"] = 0x70 + i - 1;
        for (var c = 'A'; c <= 'Z'; c++) keys[c.ToString()] = c;
        for (var d = 0; d <= 9; d++)
        {
            keys[d.ToString()] = 0x30 + d;
            keys[$"Num{d}"] = 0x60 + d;
        }
        return keys;
    }

    /// <summary>Function keys may stand alone; any other key needs a modifier so typing is never captured.</summary>
    public bool IsUsable => Modifiers != HotkeyModifiers.None || Key is >= 0x70 and <= 0x87;

    public static Hotkey? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var modifiers = HotkeyModifiers.None;
        int? key = null;
        foreach (var part in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (ModifierNames.TryGetValue(part, out var modifier))
                modifiers |= modifier;
            else if (key is null && NamedKeys.TryGetValue(part, out var code))
                key = code;
            else
                return null;
        }
        return key is { } k && new Hotkey(modifiers, k) is { IsUsable: true } hotkey ? hotkey : null;
    }

    /// <summary>The key code for a key name such as "U", "F1" or "Num0", or null when the widget does not support it.</summary>
    public static int? KeyCode(string name) => NamedKeys.TryGetValue(name, out var code) ? code : null;

    /// <summary>The key's name, or null when the widget does not support it as a shortcut key.</summary>
    public static string? KeyName(int key) => NamedKeys.FirstOrDefault(p => p.Value == key).Key;

    public override string ToString()
    {
        var parts = new[] { HotkeyModifiers.Ctrl, HotkeyModifiers.Alt, HotkeyModifiers.Shift, HotkeyModifiers.Win }
            .Where(m => Modifiers.HasFlag(m)).Select(m => m.ToString()).ToList();
        parts.Add(KeyName(Key) ?? $"0x{Key:X2}");
        return string.Join("+", parts);
    }
}
