using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using UsageWidget.Core;

namespace UsageWidget;

// The usage view, refresh loop and window behaviour; the Accounts view is in MainWindow.Accounts.cs.
public partial class MainWindow : Window
{
    readonly WidgetSettings settings;
    readonly string settingsPath;
    readonly UsageService service;
    readonly DispatcherTimer refreshTimer;
    readonly DispatcherTimer clockTimer;
    readonly TimeZoneInfo zone = TimeZoneInfo.Local;
    readonly string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // Latest Row per Account name; Render picks the ones for the Accounts currently shown.
    IReadOnlyList<Row> rows = [];
    AnnouncedReset? announcement;
    DateTimeOffset? lastRefresh;
    bool refreshing;
    bool refreshAgain;
    bool exiting;
    GlobalHotkey? hotkey;
    // Why the saved shortcut is not active, such as another app owning it; null when it works.
    string? hotkeyProblem;
    readonly CancellationTokenSource shutdown = new();

    public event Action? ExitRequested;

    public MainWindow(WidgetSettings settings, string settingsPath, UsageService service)
    {
        InitializeComponent();
        this.settings = settings;
        this.settingsPath = settingsPath;
        this.service = service;

        refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(settings.RefreshMinutes) };
        refreshTimer.Tick += (_, _) => RequestRefresh();
        clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        clockTimer.Tick += (_, _) => Render();

        SourceInitialized += (_, _) =>
        {
            NativeWindow.AddMinimizeBox(this);
            hotkey = new GlobalHotkey(this);
            hotkey.Pressed += ToggleFromHotkey;
            ApplyHotkey();
        };

        // With a taskbar button to bring the widget back, closing it (Alt+F4) exits the app.
        Closing += (_, e) =>
        {
            if (exiting) return;
            e.Cancel = true;
            Dispatcher.BeginInvoke(RequestExit);
        };

        Loaded += (_, _) =>
        {
            PlaceWindow();
            Render();
            if (settings.Accounts.Count == 0) ShowAccounts();
            RequestRefresh();
            refreshTimer.Start();
            clockTimer.Start();
        };
    }

    public async void RequestRefresh()
    {
        if (refreshing)
        {
            // The Account list may have changed while reading; read again once this pass ends.
            refreshAgain = true;
            return;
        }
        refreshing = true;
        BusyDot.Visibility = Visibility.Visible;
        try
        {
            (rows, announcement) = await service.RefreshAsync(settings.VisibleAccounts.ToList(), rows, shutdown.Token);
            lastRefresh = DateTimeOffset.Now;
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
        {
            return;
        }
        catch (Exception e)
        {
            // Keep the last Rows on screen; per-Account failures are already handled inside the service.
            rows = rows.Select(r => r.HasData ? r with { IsStale = true, Error = e.Message } : r with { Error = e.Message }).ToList();
        }
        finally
        {
            refreshing = false;
            if (!exiting)
            {
                BusyDot.Visibility = Visibility.Hidden;
                Render();
                if (refreshAgain)
                {
                    refreshAgain = false;
                    RequestRefresh();
                }
            }
        }
    }

    void Render()
    {
        var now = DateTimeOffset.Now;
        var shown = settings.VisibleAccounts.ToList();
        // The announced-reset feed is about Codex, so its line sits under the Codex Row(s).
        RowsList.ItemsSource = shown
            .Select(a => RowView.From(
                rows.FirstOrDefault(r => r.Name == a.Name) ?? Row.Empty(a.Name, null),
                AccountCatalog.Is(a, "codex") ? announcement : null, now, zone))
            .ToList();
        NoRowsHint.Visibility = shown.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdatedText.Text = lastRefresh is { } t ? $"updated {TimeZoneInfo.ConvertTime(t, zone):HH:mm}" : "loading…";
    }

    void ShowUsage()
    {
        CloseAddForm();
        AccountsView.Visibility = Visibility.Collapsed;
        UsageView.Visibility = Visibility.Visible;
        TitleText.Text = "AI usage";
        Render();
        RequestRefresh();
    }

    // --- Window ----------------------------------------------------------------------------------

    // Dragging the background moves the widget; on the usage view a press that does not move it refreshes.
    void OnPanelMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInsideControl(e.OriginalSource as DependencyObject, (DependencyObject)sender)) return;
        var before = (Left, Top);
        DragMove();
        if (before != (Left, Top)) SavePosition();
        else if (UsageView.IsVisible) RequestRefresh();
    }

    static bool IsInsideControl(DependencyObject? element, DependencyObject panel)
    {
        for (var d = element; d is not null && d != panel;
             d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
        {
            if (d is ButtonBase or TextBoxBase or ScrollBar) return true;
        }
        return false;
    }

    void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    /// <summary>The shortcut brings the widget to the front, or minimizes it when it already is in front.</summary>
    void ToggleFromHotkey()
    {
        if (WindowState != WindowState.Minimized && IsActive)
        {
            WindowState = WindowState.Minimized;
            return;
        }
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    void PlaceWindow()
    {
        var area = SystemParameters.WorkArea;
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

        if (settings.Left is { } l && settings.Top is { } t
            && l >= virtualLeft && l + 40 <= virtualRight && t >= virtualTop && t + 40 <= virtualBottom)
        {
            Left = l;
            Top = t;
        }
        else
        {
            Left = area.Right - Width - 16;
            Top = area.Top + 120;
        }
    }

    public void SavePosition()
    {
        // A minimized window reports a parking position far off screen; keep the last real one.
        if (WindowState == WindowState.Normal)
        {
            settings.Left = Math.Round(Left);
            settings.Top = Math.Round(Top);
        }
        SaveSettings();
    }

    void SaveSettings()
    {
        try
        {
            settings.Save(settingsPath);
        }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException)
        {
            // The next change or exit saves again; the widget keeps running on the in-memory settings.
        }
    }

    /// <summary>Stops timers and any running reads (CLI process trees are killed), then exits.</summary>
    public void RequestExit()
    {
        if (exiting) return;
        exiting = true;
        refreshTimer.Stop();
        clockTimer.Stop();
        hotkey?.Dispose();
        shutdown.Cancel();
        ExitRequested?.Invoke();
    }

    void OnWebLinkClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string url } || !url.StartsWith("https://", StringComparison.Ordinal)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // No default browser; the tooltip still names the page.
        }
    }

    void OnMenuOpened(object sender, RoutedEventArgs e) => StartupItem.IsChecked = StartupEntry.IsEnabled;
    void OnRefreshClick(object sender, RoutedEventArgs e) => RequestRefresh();
    void OnStartupClick(object sender, RoutedEventArgs e) => StartupEntry.Set(StartupItem.IsChecked);
    void OnExitClick(object sender, RoutedEventArgs e) => RequestExit();
}
