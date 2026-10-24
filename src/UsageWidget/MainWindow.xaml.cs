using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using UsageWidget.Core;

namespace UsageWidget;

public partial class MainWindow : Window
{
    readonly WidgetSettings settings;
    readonly string settingsPath;
    readonly UsageService service;
    readonly DispatcherTimer refreshTimer;
    readonly DispatcherTimer clockTimer;
    readonly TimeZoneInfo zone = TimeZoneInfo.Local;

    IReadOnlyList<Row> rows;
    AnnouncedReset? announcement;
    DateTimeOffset? lastRefresh;
    bool refreshing;
    bool exiting;
    readonly CancellationTokenSource shutdown = new();

    public event Action? ExitRequested;

    public MainWindow(WidgetSettings settings, string settingsPath, UsageService service)
    {
        InitializeComponent();
        this.settings = settings;
        this.settingsPath = settingsPath;
        this.service = service;
        rows = settings.Accounts.Select(a => Row.Empty(a.Name, null)).ToList();

        refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(settings.RefreshMinutes) };
        refreshTimer.Tick += (_, _) => RequestRefresh();
        clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        clockTimer.Tick += (_, _) => Render();

        // Alt+F4 hides instead of closing, so the tray icon can always bring the widget back.
        Closing += (_, e) =>
        {
            if (exiting) return;
            e.Cancel = true;
            Hide();
        };

        Loaded += (_, _) =>
        {
            PlaceWindow();
            Render();
            RequestRefresh();
            refreshTimer.Start();
            clockTimer.Start();
        };
    }

    public async void RequestRefresh()
    {
        if (refreshing) return;
        refreshing = true;
        BusyDot.Visibility = Visibility.Visible;
        try
        {
            (rows, announcement) = await service.RefreshAsync(rows, shutdown.Token);
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
            }
        }
    }

    void Render()
    {
        var now = DateTimeOffset.Now;
        // The announced-reset feed is about Codex, so its line sits under the Codex Row(s).
        RowsList.ItemsSource = rows
            .Select((r, i) => RowView.From(
                r, settings.Accounts.ElementAtOrDefault(i)?.Provider == "codex" ? announcement : null, now, zone))
            .ToList();
        UpdatedText.Text = lastRefresh is { } t ? $"updated {TimeZoneInfo.ConvertTime(t, zone):HH:mm}" : "loading…";
    }

    // Dragging moves the widget; a press that does not move it counts as a click and refreshes.
    void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        var before = (Left, Top);
        DragMove();
        if (before == (Left, Top)) RequestRefresh();
        else SavePosition();
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
        settings.Left = Math.Round(Left);
        settings.Top = Math.Round(Top);
        try
        {
            settings.Save(settingsPath);
        }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException)
        {
            // Position is a convenience; losing one save is harmless.
        }
    }

    public void ToggleVisible()
    {
        if (IsVisible) Hide();
        else
        {
            Show();
            Topmost = true;
        }
    }

    /// <summary>Stops timers and any running codexbar reads (their process trees are killed), then exits.</summary>
    public void RequestExit()
    {
        exiting = true;
        refreshTimer.Stop();
        clockTimer.Stop();
        shutdown.Cancel();
        ExitRequested?.Invoke();
    }

    void OnMenuOpened(object sender, RoutedEventArgs e) => StartupItem.IsChecked = StartupEntry.IsEnabled;
    void OnRefreshClick(object sender, RoutedEventArgs e) => RequestRefresh();
    void OnHideClick(object sender, RoutedEventArgs e) => Hide();
    void OnStartupClick(object sender, RoutedEventArgs e) => StartupEntry.Set(StartupItem.IsChecked);
    void OnExitClick(object sender, RoutedEventArgs e) => RequestExit();
}
