using System.IO;
using System.Net.Http;
using System.Windows;
using UsageWidget.Core;

namespace UsageWidget;

public partial class App : Application
{
    Mutex? singleInstance;
    MainWindow? window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // A widget that silently disappears is worse than one that skips a frame.
        DispatcherUnhandledException += (_, args) =>
        {
            LogError(args.Exception);
            args.Handled = true;
        };

        singleInstance = new Mutex(true, @"Local\UsageWidget.SingleInstance", out var first);
        if (!first)
        {
            Shutdown();
            return;
        }

        try
        {
            var settingsPath = WidgetSettings.DefaultPath;
            var settings = WidgetSettings.Load(settingsPath, () => WidgetSettings.DetectAccounts());
            var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"UsageWidget/{typeof(App).Assembly.GetName().Version?.ToString(3)}");

            window = new MainWindow(settings, settingsPath, new UsageService(settings, http));
            window.ExitRequested += ExitApp;
            window.Show();
        }
        catch (Exception error)
        {
            // Without a window there is nothing to keep running for; leave a trace instead of a ghost process.
            LogError(error);
            singleInstance.ReleaseMutex();
            Shutdown(1);
        }
    }

    static void LogError(Exception error)
    {
        try
        {
            var path = Path.Combine(Path.GetDirectoryName(WidgetSettings.DefaultPath)!, "error.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, $"{DateTimeOffset.Now:O} {error}{Environment.NewLine}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Logging is best effort.
        }
    }

    void ExitApp()
    {
        window?.SavePosition();
        singleInstance?.ReleaseMutex();
        Shutdown();
    }
}
