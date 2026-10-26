using Avalonia;
using Avalonia.Controls;

namespace UsageWidget;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        // One widget per user: a second launch exits before any window exists.
        using var singleInstance = new Mutex(true, @"Local\UsageWidget.SingleInstance", out var first);
        if (!first) return 0;
        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
        }
        finally
        {
            singleInstance.ReleaseMutex();
        }
    }

    static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
