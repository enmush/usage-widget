using System.IO;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using UsageWidget.Core;

namespace UsageWidget;

public partial class App : Application
{
    MainWindow? window;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        base.OnFrameworkInitializationCompleted();
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;

        // A widget that silently disappears is worse than one that skips a frame.
        Dispatcher.UIThread.UnhandledException += (_, args) =>
        {
            LogError(args.Exception);
            args.Handled = true;
        };

        try
        {
            var settingsPath = WidgetSettings.DefaultPath;
            var settings = WidgetSettings.Load(settingsPath, () => WidgetSettings.DetectAccounts());
            var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"UsageWidget/{typeof(App).Assembly.GetName().Version?.ToString(3)}");

            window = new MainWindow(settings, settingsPath, new UsageService(settings, http));
            window.ExitRequested += () => desktop.Shutdown();
            // Quit from the macOS app menu or Dock, or signing out: save and stop reads first.
            desktop.ShutdownRequested += (_, e) =>
            {
                if (window.IsExiting) return;
                e.Cancel = true;
                window.RequestExit();
            };
            // Clicking the Dock icon brings back a widget hidden by minimizing.
            if (TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime activatable)
                activatable.Activated += (_, e) =>
                {
                    if (e.Kind == ActivationKind.Reopen) window.BringBack();
                };
            desktop.MainWindow = window;
            window.Show();
        }
        catch (Exception error)
        {
            // Without a window there is nothing to keep running for; leave a trace instead of a ghost process.
            LogError(error);
            desktop.Shutdown(1);
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
}
