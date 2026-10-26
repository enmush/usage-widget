using System.Security;

namespace UsageWidget.Core;

/// <summary>"Start at login" on macOS: a LaunchAgent in the user's LaunchAgents folder that opens the app.</summary>
public static class MacLaunchAgent
{
    public const string Label = "io.github.enmush.usage-widget";

    public static string PathFor(string home) => Path.Combine(home, "Library", "LaunchAgents", Label + ".plist");

    /// <param name="processPath">The running executable; inside an app bundle the bundle is opened instead.</param>
    public static string Plist(string processPath)
    {
        const string marker = ".app/Contents/MacOS/";
        var bundle = processPath.IndexOf(marker, StringComparison.Ordinal) is var i and >= 0 ? processPath[..(i + 4)] : null;
        string[] program = bundle is null ? [processPath] : ["/usr/bin/open", bundle];
        var arguments = string.Join("\n", program.Select(arg => $"        <string>{SecurityElement.Escape(arg)}</string>"));
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>Label</key>
                <string>{Label}</string>
                <key>ProgramArguments</key>
                <array>
            {arguments}
                </array>
                <key>RunAtLoad</key>
                <true/>
            </dict>
            </plist>

            """.Replace("\r\n", "\n");
    }
}
