using UsageWidget.Core;

namespace UsageWidget.Tests;

public class SystemPathTests
{
    [Fact]
    public void Mac_folders_take_the_app_path_then_the_login_shell_then_usual_install_folders()
    {
        var folders = SystemPath.MacFolders("/usr/bin:/bin", "/Users/jun/bin:/usr/bin:/opt/homebrew/bin", "/Users/jun");

        Assert.Equal(
            ["/usr/bin", "/bin", "/Users/jun/bin", "/opt/homebrew/bin", "/Users/jun/.local/bin", "/usr/local/bin",
             "/Users/jun/google-cloud-sdk/bin", "/opt/homebrew/share/google-cloud-sdk/bin", "/usr/local/share/google-cloud-sdk/bin"],
            folders);
    }

    [Fact]
    public void Mac_folders_work_without_a_login_shell_answer()
    {
        var folders = SystemPath.MacFolders(null, null, "/Users/jun");

        Assert.Equal("/Users/jun/.local/bin", folders[0]);
    }

    [Theory]
    [InlineData("Last login: today\n__UW__/usr/bin:/bin__UW__\n", "/usr/bin:/bin")]
    [InlineData("__UW____UW__", "")]
    [InlineData("no markers here", null)]
    public void Shell_answer_is_the_text_between_the_markers(string output, string? expected)
    {
        Assert.Equal(expected, SystemPath.BetweenMarkers(output));
    }
}
