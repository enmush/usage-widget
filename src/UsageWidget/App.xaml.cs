using System.Net.Http;
using System.Windows;
using UsageWidget.Core;

namespace UsageWidget;

public partial class App : Application
{
    Mutex? singleInstance;
    TrayIcon? tray;
    MainWindow? window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        singleInstance = new Mutex(true, @"Local\UsageWidget.SingleInstance", out var first);
        if (!first)
        {
            Shutdown();
            return;
        }

        var settingsPath = WidgetSettings.DefaultPath;
        var settings = WidgetSettings.Load(settingsPath);
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("UsageWidget/0.1");

        window = new MainWindow(settings, settingsPath, new UsageService(settings, http));
        tray = new TrayIcon(window);
        window.ExitRequested += ExitApp;
        window.Show();
    }

    void ExitApp()
    {
        window?.SavePosition();
        tray?.Dispose();
        singleInstance?.ReleaseMutex();
        Shutdown();
    }
}
