using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
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
    readonly string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // Latest Row per Account name; Render picks the ones for the Accounts currently shown.
    IReadOnlyList<Row> rows = [];
    AnnouncedReset? announcement;
    DateTimeOffset? lastRefresh;
    bool refreshing;
    bool refreshAgain;
    bool exiting;
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

        SourceInitialized += (_, _) => AddMinimizeStyle();

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

    // --- Accounts view ---------------------------------------------------------------------------

    void ShowAccounts()
    {
        UsageView.Visibility = Visibility.Collapsed;
        AccountsView.Visibility = Visibility.Visible;
        TitleText.Text = "Accounts";
        RenderAccounts();
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

    const string ConsentHint = "Claude Accounts need: codexbar-cli config claude-code-credentials allow";

    /// <param name="note">The outcome of the last action, shown before the standing hint.</param>
    void RenderAccounts(string? note = null)
    {
        AccountsList.ItemsSource = settings.Accounts.Select((a, i) => AccountItem.From(a, i, home)).ToList();
        var hint = settings.Accounts.Count == 0
            ? "No sign-ins found on this PC. Sign in with claude, codex or agy and press Detect logins, or add an Account below. "
            : "Unchecked Accounts stay saved but get no Row. ";
        AccountsHint.Text = (note is null ? "" : note + " ") + hint + ConsentHint;
    }

    void ChangeAccounts(List<AccountSettings> accounts, string? note = null)
    {
        settings.Accounts = accounts;
        SaveSettings();
        RenderAccounts(note);
    }

    void OnAccountsClick(object sender, RoutedEventArgs e)
    {
        if (AccountsView.IsVisible) ShowUsage();
        else ShowAccounts();
    }

    void OnDoneClick(object sender, RoutedEventArgs e) => ShowUsage();

    // Checked/Unchecked rather than Click, so keyboard and UI Automation toggles are saved too.
    // They also fire when a list item is first bound, which the equality check ignores.
    void OnShowToggled(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { Tag: int index } box || index >= settings.Accounts.Count) return;
        var hidden = box.IsChecked != true;
        if (settings.Accounts[index].Hidden == hidden) return;
        // The checkbox already shows the new state, so the list is saved without being rebuilt under it.
        settings.Accounts = settings.Accounts.Select((a, i) => i == index ? a with { Hidden = hidden } : a).ToList();
        SaveSettings();
    }

    void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int index } || index >= settings.Accounts.Count) return;
        var accounts = settings.Accounts.ToList();
        var removed = accounts[index];
        accounts.RemoveAt(index);
        // A later Account given the same name must not inherit this one's last Row.
        rows = rows.Where(r => !AccountCatalog.SameName(r.Name, removed.Name)).ToList();
        ChangeAccounts(accounts);
    }

    void OnDetectClick(object sender, RoutedEventArgs e)
    {
        var merged = LoginDetector.Merge(settings.Accounts, WidgetSettings.DetectAccounts(), home);
        var added = merged.Count - settings.Accounts.Count;
        ChangeAccounts(merged, added == 0 ? "No new sign-ins found." : $"Added {added} Account{(added == 1 ? "" : "s")}.");
    }

    void OnAddClick(object sender, RoutedEventArgs e)
    {
        NameBox.Text = "";
        FolderBox.Text = "";
        AddError.Visibility = Visibility.Collapsed;
        ProviderClaude.IsChecked = true;
        UpdateFolderRow();
        AddForm.Visibility = Visibility.Visible;
        AddButton.IsEnabled = false;
        NameBox.Focus();
    }

    void OnCancelAddClick(object sender, RoutedEventArgs e) => CloseAddForm();

    void CloseAddForm()
    {
        AddForm.Visibility = Visibility.Collapsed;
        AddButton.IsEnabled = true;
    }

    void OnProviderChanged(object sender, RoutedEventArgs e)
    {
        // Fires while the window is still being built, before the folder controls exist.
        if (FolderRow is not null) UpdateFolderRow();
    }

    // Each pill's Tag is a Provider id from the catalog.
    Provider SelectedProvider =>
        AccountCatalog.Find(ProviderPicker.Children.OfType<RadioButton>().FirstOrDefault(r => r.IsChecked == true)?.Tag as string ?? "")
        ?? AccountCatalog.Providers[0];

    void UpdateFolderRow()
    {
        var provider = SelectedProvider;
        FolderRow.IsEnabled = provider.LoginFolderVariable is not null;
        // The flat field and button styles have no disabled look of their own.
        FolderRow.Opacity = FolderRow.IsEnabled ? 1 : 0.4;
        FolderHelp.Text = provider.LoginFolderVariable is { } variable
            ? $"Empty means ~\\{provider.DefaultFolderName}. A second sign-in lives in its own folder: run the CLI with {variable} set to it."
            : $"{provider.DisplayName} uses its own CLI sign-in; no folder needed.";
    }

    void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Login folder", InitialDirectory = home };
        if (dialog.ShowDialog(this) == true) FolderBox.Text = dialog.FolderName;
    }

    void OnConfirmAddClick(object sender, RoutedEventArgs e)
    {
        var (account, error) = AccountCatalog.Create(
            NameBox.Text, SelectedProvider.Id, FolderRow.IsEnabled ? FolderBox.Text : null, home);
        if (account is not null && settings.Accounts.Any(a => AccountCatalog.SameName(a.Name, account.Name)))
            error = "That name is already used.";
        else if (account is not null && settings.Accounts.FirstOrDefault(a => AccountCatalog.SameLogin(a, account, home)) is { } same)
            error = $"That sign-in is already listed as {same.Name}.";

        if (error is not null)
        {
            AddError.Text = error;
            AddError.Visibility = Visibility.Visible;
            return;
        }
        ChangeAccounts([.. settings.Accounts, account!]);
        CloseAddForm();
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

    const int GwlStyle = -16;
    const nint WsMinimizeBox = 0x00020000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    static extern nint GetWindowLongPtr(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    static extern nint SetWindowLongPtr(nint window, int index, nint value);

    // A borderless WPF window lacks the minimize style, so its taskbar button would only restore it, never minimize.
    void AddMinimizeStyle()
    {
        var handle = new WindowInteropHelper(this).Handle;
        SetWindowLongPtr(handle, GwlStyle, GetWindowLongPtr(handle, GwlStyle) | WsMinimizeBox);
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

    /// <summary>Stops timers and any running codexbar reads (their process trees are killed), then exits.</summary>
    public void RequestExit()
    {
        if (exiting) return;
        exiting = true;
        refreshTimer.Stop();
        clockTimer.Stop();
        shutdown.Cancel();
        ExitRequested?.Invoke();
    }

    void OnMenuOpened(object sender, RoutedEventArgs e) => StartupItem.IsChecked = StartupEntry.IsEnabled;
    void OnRefreshClick(object sender, RoutedEventArgs e) => RequestRefresh();
    void OnStartupClick(object sender, RoutedEventArgs e) => StartupEntry.Set(StartupItem.IsChecked);
    void OnExitClick(object sender, RoutedEventArgs e) => RequestExit();
}
