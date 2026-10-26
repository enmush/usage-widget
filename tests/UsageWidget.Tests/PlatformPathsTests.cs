using UsageWidget.Core;

namespace UsageWidget.Tests;

public class PlatformPathsTests
{
    [Fact]
    public void Settings_live_in_application_support_on_macos()
    {
        var folder = WidgetSettings.Folder(isMac: true, home: "/Users/jun", appData: "/Users/jun/.config");

        Assert.Equal(Path.Combine("/Users/jun", "Library", "Application Support", "UsageWidget"), folder);
    }

    [Fact]
    public void Settings_live_in_roaming_app_data_on_windows()
    {
        var folder = WidgetSettings.Folder(isMac: false, home: @"C:\Users\jun", appData: @"C:\Users\jun\AppData\Roaming");

        Assert.Equal(Path.Combine(@"C:\Users\jun\AppData\Roaming", "UsageWidget"), folder);
    }
}
