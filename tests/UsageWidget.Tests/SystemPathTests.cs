using UsageWidget.Core;

namespace UsageWidget.Tests;

public class SystemPathTests
{
    [Fact]
    public void Mac_folders_take_the_app_path_then_usual_install_folders_then_the_login_shell()
    {
        var folders = SystemPath.MacFolders("/usr/bin:/bin", "/Users/jun/bin:/usr/bin:/opt/homebrew/bin", "/Users/jun");

        Assert.Equal(
            ["/usr/bin", "/bin", "/Users/jun/.local/bin", "/opt/homebrew/bin", "/usr/local/bin", "/Users/jun/google-cloud-sdk/bin",
             "/opt/homebrew/share/google-cloud-sdk/bin", "/usr/local/share/google-cloud-sdk/bin", "/Users/jun/bin"],
            folders);
    }

    [Fact]
    public void Mac_folders_work_without_a_login_shell_answer()
    {
        var folders = SystemPath.MacFolders(null, null, "/Users/jun");

        Assert.Equal("/Users/jun/.local/bin", folders[0]);
    }

    [Fact]
    public void Login_shell_is_asked_only_when_the_tool_is_elsewhere()
    {
        var asked = 0;
        string? Shell() { asked++; return "/Users/jun/tools"; }
        bool Exists(string path) => path is "/opt/homebrew/bin/gcloud" or "/Users/jun/tools/agy";

        Assert.Equal("/opt/homebrew/bin/gcloud", SystemPath.FindOnMac("gcloud", "/usr/bin", "/Users/jun", Shell, Exists));
        Assert.Equal(0, asked);
        Assert.Equal("/Users/jun/tools/agy", SystemPath.FindOnMac("agy", "/usr/bin", "/Users/jun", Shell, Exists));
        Assert.Equal(1, asked);
        Assert.Null(SystemPath.FindOnMac("missing", "/usr/bin", "/Users/jun", Shell, Exists));
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
