using UsageWidget.Core;

namespace UsageWidget.Tests;

public class MacShortcutTests
{
    [Theory]
    [InlineData("Ctrl+Alt+Shift+U", "⌃⌥⇧U")]
    [InlineData("Win+F5", "⌘F5")]
    [InlineData("Ctrl+Space", "⌃Space")]
    public void Mac_shows_modifier_symbols(string text, string shown)
    {
        Assert.Equal(shown, Hotkey.Parse(text)!.Describe(mac: true));
    }

    [Fact]
    public void Windows_shows_the_settings_text() =>
        Assert.Equal("Ctrl+Alt+Shift+U", Hotkey.Parse("Ctrl+Alt+Shift+U")!.Describe(mac: false));

    [Theory]
    [InlineData("U", 0x20)]
    [InlineData("A", 0x00)]
    [InlineData("0", 0x1D)]
    [InlineData("5", 0x17)]
    [InlineData("F1", 0x7A)]
    [InlineData("F12", 0x6F)]
    [InlineData("Num0", 0x52)]
    [InlineData("Delete", 0x75)]
    [InlineData("Up", 0x7E)]
    [InlineData("`", 0x32)]
    public void Keys_map_to_carbon_key_codes(string key, int code)
    {
        Assert.Equal((uint)code, Hotkey.Parse("Ctrl+" + key)!.MacKeyCode);
    }

    [Theory]
    [InlineData("Ctrl+F24")]
    [InlineData("Ctrl+Pause")]
    public void Keys_a_mac_keyboard_lacks_have_no_code(string text) => Assert.Null(Hotkey.Parse(text)!.MacKeyCode);

    [Fact]
    public void Modifiers_map_to_carbon_flags()
    {
        Assert.Equal(0x1000u | 0x0800u | 0x0200u, Hotkey.Parse("Ctrl+Alt+Shift+U")!.MacModifiers);
        Assert.Equal(0x0100u, Hotkey.Parse("Win+U")!.MacModifiers);
    }

    [Fact]
    public void Launch_agent_opens_the_app_bundle_at_login()
    {
        var plist = MacLaunchAgent.Plist("/Applications/Usage Widget.app/Contents/MacOS/UsageWidget");

        Assert.Contains("<string>io.github.enmush.usage-widget</string>", plist);
        Assert.Contains("<string>/usr/bin/open</string>", plist);
        Assert.Contains("<string>/Applications/Usage Widget.app</string>", plist);
        Assert.Contains("<key>RunAtLoad</key>\n    <true/>", plist);
    }

    [Fact]
    public void Launch_agent_runs_a_bare_executable_directly()
    {
        var plist = MacLaunchAgent.Plist("/Users/jun/dev/UsageWidget");

        Assert.DoesNotContain("/usr/bin/open", plist);
        Assert.Contains("<string>/Users/jun/dev/UsageWidget</string>", plist);
    }

    [Fact]
    public void Launch_agent_escapes_xml()
    {
        Assert.Contains("A&amp;B.app", MacLaunchAgent.Plist("/Applications/A&B.app/Contents/MacOS/UsageWidget"));
    }
}
