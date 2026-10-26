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

    /// <summary>The shortcut as each platform writes it: "Ctrl+Alt+Shift+U", or "⌃⌥⇧U" on a Mac, where Win is ⌘.</summary>
    public string Describe(bool mac)
    {
        if (!mac) return ToString();
        var symbols = new[] { (HotkeyModifiers.Ctrl, "⌃"), (HotkeyModifiers.Alt, "⌥"), (HotkeyModifiers.Shift, "⇧"), (HotkeyModifiers.Win, "⌘") }
            .Where(m => Modifiers.HasFlag(m.Item1)).Select(m => m.Item2);
        return string.Concat(symbols) + (KeyName(Key) ?? $"0x{Key:X2}");
    }

    /// <summary>Carbon modifier flags: ⌘ 0x100, ⇧ 0x200, ⌥ 0x800, ⌃ 0x1000.</summary>
    public uint MacModifiers =>
        (Modifiers.HasFlag(HotkeyModifiers.Win) ? 0x0100u : 0) | (Modifiers.HasFlag(HotkeyModifiers.Shift) ? 0x0200u : 0)
        | (Modifiers.HasFlag(HotkeyModifiers.Alt) ? 0x0800u : 0) | (Modifiers.HasFlag(HotkeyModifiers.Ctrl) ? 0x1000u : 0);

    /// <summary>The Carbon virtual key code (US layout position) for the key, or null when Mac keyboards lack it.</summary>
    public uint? MacKeyCode => MacKeyCodes.TryGetValue(Key, out var code) ? code : null;

    // From Carbon's Events.h (kVK_*), keyed by the Win32 virtual-key code the settings use.
    static readonly Dictionary<int, uint> MacKeyCodes = new()
    {
        ['A'] = 0x00, ['S'] = 0x01, ['D'] = 0x02, ['F'] = 0x03, ['H'] = 0x04, ['G'] = 0x05, ['Z'] = 0x06, ['X'] = 0x07,
        ['C'] = 0x08, ['V'] = 0x09, ['B'] = 0x0B, ['Q'] = 0x0C, ['W'] = 0x0D, ['E'] = 0x0E, ['R'] = 0x0F, ['Y'] = 0x10,
        ['T'] = 0x11, ['O'] = 0x1F, ['U'] = 0x20, ['I'] = 0x22, ['P'] = 0x23, ['L'] = 0x25, ['J'] = 0x26, ['K'] = 0x28,
        ['N'] = 0x2D, ['M'] = 0x2E,
        ['1'] = 0x12, ['2'] = 0x13, ['3'] = 0x14, ['4'] = 0x15, ['6'] = 0x16, ['5'] = 0x17, ['9'] = 0x19, ['7'] = 0x1A,
        ['8'] = 0x1C, ['0'] = 0x1D,
        [0x60] = 0x52, [0x61] = 0x53, [0x62] = 0x54, [0x63] = 0x55, [0x64] = 0x56, [0x65] = 0x57, [0x66] = 0x58,
        [0x67] = 0x59, [0x68] = 0x5B, [0x69] = 0x5C,
        [0x70] = 0x7A, [0x71] = 0x78, [0x72] = 0x63, [0x73] = 0x76, [0x74] = 0x60, [0x75] = 0x61, [0x76] = 0x62,
        [0x77] = 0x64, [0x78] = 0x65, [0x79] = 0x6D, [0x7A] = 0x67, [0x7B] = 0x6F, [0x7C] = 0x69, [0x7D] = 0x6B,
        [0x7E] = 0x71, [0x7F] = 0x6A, [0x80] = 0x40, [0x81] = 0x4F, [0x82] = 0x50, [0x83] = 0x5A,
        [0x20] = 0x31, [0x21] = 0x74, [0x22] = 0x79, [0x23] = 0x77, [0x24] = 0x73, [0x25] = 0x7B, [0x26] = 0x7E,
        [0x27] = 0x7C, [0x28] = 0x7D, [0x2D] = 0x72, [0x2E] = 0x75, [0xC0] = 0x32,
    };
}
