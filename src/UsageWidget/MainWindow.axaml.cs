using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using UsageWidget.Core;
using UsageWidget.Platform;

namespace UsageWidget;

// The usage view, refresh loop and window behaviour; the Accounts view is in MainWindow.Accounts.cs.
public partial class MainWindow : Window
{
    readonly WidgetSettings settings;
    readonly string settingsPath;
    readonly UsageService service;
    readonly IPlatform platform = IPlatform.Current;
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
    // Why the saved shortcut is not active, such as another app owning it; null when it works.
    string? hotkeyProblem;
    readonly CancellationTokenSource shutdown = new();

    // A press on the background either drags the widget or, if it does not move, refreshes.
    PixelPoint? dragStart;
    PixelPoint windowStart;
    bool dragged;

    public event Action? ExitRequested;
    public bool IsExiting { get; private set; }

    // For the XAML previewer only.
    public MainWindow() : this(new WidgetSettings(), "", new UsageService(new WidgetSettings(), new HttpClient())) { }

    public MainWindow(WidgetSettings settings, string settingsPath, UsageService service)
    {
        InitializeComponent();
        this.settings = settings;
        this.settingsPath = settingsPath;
        this.service = service;
        StartupItem.Header = platform.StartAtLoginLabel;

        refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(settings.RefreshMinutes) };
        refreshTimer.Tick += (_, _) => RequestRefresh();
        clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        clockTimer.Tick += (_, _) => Render();

        Panel.PointerPressed += OnPanelPressed;
        Panel.PointerMoved += OnPanelMoved;
        Panel.PointerReleased += OnPanelReleased;
        HotkeyBox.AddHandler(KeyDownEvent, OnHotkeyKeyDown, RoutingStrategies.Tunnel);
        HotkeyBox.GotFocus += OnHotkeyFocus;
        HotkeyBox.LostFocus += OnHotkeyBlur;
        platform.HotkeyPressed += ToggleFromHotkey;

        PlaceWindow();
        Opened += (_, _) =>
        {
            platform.Attach(this);
            ApplyHotkey();
            Render();
            if (settings.Accounts.Count == 0) ShowAccounts();
            RequestRefresh();
            refreshTimer.Start();
            clockTimer.Start();
        };

        // With a taskbar button or Dock icon to bring the widget back, closing it (Alt+F4) exits the app.
        Closing += (_, e) =>
        {
            if (IsExiting) return;
            e.Cancel = true;
            Dispatcher.UIThread.Post(RequestExit);
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
        BusyDot.Opacity = 1;
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
            if (!IsExiting)
            {
                BusyDot.Opacity = 0;
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
            .Select(a => new RowView(RowCells.From(
                rows.FirstOrDefault(r => r.Name == a.Name) ?? Row.Empty(a.Name, null),
                AccountCatalog.Is(a, "codex") ? announcement : null, now, zone)))
            .ToList();
        NoRowsHint.IsVisible = shown.Count == 0;
        UpdatedText.Text = lastRefresh is { } t ? $"updated {TimeZoneInfo.ConvertTime(t, zone):HH:mm}" : "loading…";
    }

    void ShowUsage()
    {
        CloseAddForm();
        AccountsView.IsVisible = false;
        UsageView.IsVisible = true;
        TitleText.Text = "AI usage";
        Render();
        RequestRefresh();
    }

    // --- Window ----------------------------------------------------------------------------------

    // Buttons and text boxes handle their own presses, so only the background reaches these handlers.
    void OnPanelPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        dragStart = this.PointToScreen(e.GetPosition(this));
        windowStart = Position;
        dragged = false;
        e.Pointer.Capture(Panel);
    }

    void OnPanelMoved(object? sender, PointerEventArgs e)
    {
        if (dragStart is not { } start) return;
        var now = this.PointToScreen(e.GetPosition(this));
        var delta = now - start;
        if (!dragged && Math.Abs(delta.X) + Math.Abs(delta.Y) < 4) return;
        dragged = true;
        Position = windowStart + delta;
    }

    void OnPanelReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (dragStart is null) return;
        dragStart = null;
        e.Pointer.Capture(null);
        if (dragged) SavePosition();
        else if (UsageView.IsVisible) RequestRefresh();
    }

    void OnMinimizeClick(object? sender, RoutedEventArgs e) => platform.Minimize(this);

    /// <summary>The shortcut brings the widget to the front, or minimizes it when it already is in front.</summary>
    void ToggleFromHotkey()
    {
        if (!platform.IsMinimized(this) && IsActive) platform.Minimize(this);
        else platform.Restore(this);
    }

    /// <summary>Shows the widget again after it was minimized, such as from the Dock icon.</summary>
    public void BringBack() => platform.Restore(this);

    // Settings keep the position in device-independent units, so it survives a change of display scaling.
    void PlaceWindow()
    {
        var screens = Screens.All;
        var primary = Screens.Primary ?? screens.FirstOrDefault();
        if (primary is null) return;
        var scale = primary.Scaling;

        if (settings.Left is { } l && settings.Top is { } t)
        {
            var at = new PixelPoint((int)Math.Round(l * scale), (int)Math.Round(t * scale));
            var corner = at + new PixelVector((int)(40 * scale), (int)(40 * scale));
            if (screens.Any(s => s.Bounds.Contains(at)) && screens.Any(s => s.Bounds.Contains(corner)))
            {
                Position = at;
                return;
            }
        }
        var area = primary.WorkingArea;
        Position = new PixelPoint(area.Right - (int)((Width + 16) * scale), area.Y + (int)(120 * scale));
    }

    public void SavePosition()
    {
        // A minimized window reports a parking position far off screen; keep the last real one.
        if (!platform.IsMinimized(this) && WindowState == WindowState.Normal)
        {
            var scale = DesktopScaling;
            settings.Left = Math.Round(Position.X / scale);
            settings.Top = Math.Round(Position.Y / scale);
        }
        SaveSettings();
    }

    void SaveSettings()
    {
        try
        {
            settings.Save(settingsPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The next change or exit saves again; the widget keeps running on the in-memory settings.
        }
    }

    /// <summary>Saves the position and stops timers and any running reads, then exits.</summary>
    public void RequestExit()
    {
        if (!PrepareExit()) return;
        ExitRequested?.Invoke();
    }

    /// <summary>Saves the position and stops timers and any running reads (CLI process trees are killed); false if already done.</summary>
    public bool PrepareExit()
    {
        if (IsExiting) return false;
        SavePosition();
        IsExiting = true;
        refreshTimer.Stop();
        clockTimer.Stop();
        platform.Dispose();
        shutdown.Cancel();
        return true;
    }

    async void OnWebLinkClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: string url } || !url.StartsWith("https://", StringComparison.Ordinal)) return;
        // Without a default browser nothing opens; the tooltip still names the page.
        await Launcher.LaunchUriAsync(new Uri(url));
    }

    void OnMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e) => StartupItem.IsChecked = platform.StartsAtLogin;
    void OnRefreshClick(object? sender, RoutedEventArgs e) => RequestRefresh();

    void OnStartupClick(object? sender, RoutedEventArgs e)
    {
        platform.SetStartAtLogin(!platform.StartsAtLogin);
        StartupItem.IsChecked = platform.StartsAtLogin;
    }

    void OnExitClick(object? sender, RoutedEventArgs e) => RequestExit();
}
