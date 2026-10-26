using UsageWidget.Core;

namespace UsageWidget.Tests;

public class HotkeyTests
{
    [Theory]
    [InlineData("Ctrl+F1", HotkeyModifiers.Ctrl, 0x70, "Ctrl+F1")]
    [InlineData("shift + alt + ctrl + u", HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Shift, 0x55, "Ctrl+Alt+Shift+U")]
    [InlineData("Control+1", HotkeyModifiers.Ctrl, 0x31, "Ctrl+1")]
    [InlineData("F9", HotkeyModifiers.None, 0x78, "F9")]
    [InlineData("Win+Num5", HotkeyModifiers.Win, 0x65, "Win+Num5")]
    public void Shortcut_text_round_trips(string text, HotkeyModifiers modifiers, int key, string shown)
    {
        var hotkey = Hotkey.Parse(text);

        Assert.Equal(new Hotkey(modifiers, key), hotkey);
        Assert.Equal(shown, hotkey!.ToString());
        Assert.Equal(hotkey, Hotkey.Parse(shown));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("U")]
    [InlineData("Ctrl")]
    [InlineData("Ctrl+F1+F2")]
    [InlineData("Ctrl+Banana")]
    [InlineData("Hyper+F1")]
    public void Unusable_or_unknown_shortcuts_parse_to_nothing(string? text)
    {
        Assert.Null(Hotkey.Parse(text));
    }

    [Fact]
    public void Default_shortcut_is_valid_and_a_cleared_one_stays_cleared()
    {
        Assert.NotNull(Hotkey.Parse(WidgetSettings.DefaultHotkey));

        var path = Path.Combine(Directory.CreateTempSubdirectory("uw-hotkey-").FullName, "settings.json");
        new WidgetSettings { Hotkey = "" }.Save(path);

        Assert.Equal("", WidgetSettings.Load(path, () => []).Hotkey);
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }

    [Theory]
    [InlineData("U", 0x55)]
    [InlineData("F1", 0x70)]
    [InlineData("Num0", 0x60)]
    [InlineData("`", 0xC0)]
    public void Key_names_map_to_their_key_codes(string name, int code)
    {
        Assert.Equal(code, Hotkey.KeyCode(name));
        Assert.Equal(name, Hotkey.KeyName(code));
    }

    [Fact]
    public void Unsupported_key_name_has_no_code() => Assert.Null(Hotkey.KeyCode("Escape"));
}
