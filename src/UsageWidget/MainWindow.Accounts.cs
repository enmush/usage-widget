using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using UsageWidget.Core;

namespace UsageWidget;

// The Accounts view: show/hide, remove, add and detect. Every change is saved at once.
public partial class MainWindow
{
    void ShowAccounts()
    {
        UsageView.Visibility = Visibility.Collapsed;
        AccountsView.Visibility = Visibility.Visible;
        TitleText.Text = "Accounts";
        RenderAccounts();
    }

    /// <param name="note">The outcome of the last action, shown before the standing hint.</param>
    void RenderAccounts(string? note = null)
    {
        AccountsList.ItemsSource = settings.Accounts.Select((a, i) => AccountItem.From(a, i, home)).ToList();
        var hint = settings.Accounts.Count == 0
            ? "No sign-ins found on this PC. Sign in with claude, codex or agy and press Detect logins, or add an Account below."
            : "Unchecked Accounts stay saved but get no Row.";
        AccountsHint.Text = (note is null ? "" : note + " ") + hint;
        RenderHotkey();
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

    async void OnDetectClick(object sender, RoutedEventArgs e)
    {
        // Looking up the Gemini API key's project runs gcloud, which takes a few seconds.
        var button = (Button)sender;
        button.IsEnabled = false;
        List<AccountSettings> detected;
        try
        {
            detected = await Task.Run(() => WidgetSettings.DetectAccounts(includeGeminiApi: true));
        }
        finally
        {
            button.IsEnabled = true;
        }
        var merged = LoginDetector.Merge(settings.Accounts, detected, home);
        var added = merged.Count - settings.Accounts.Count;
        ChangeAccounts(merged, added == 0 ? "No new sign-ins found." : $"Added {added} Account{(added == 1 ? "" : "s")}.");
    }

    // --- Add form --------------------------------------------------------------------------------

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
        var isProject = provider.ProjectVariable is not null;
        FolderRow.IsEnabled = provider.LoginFolderVariable is not null || isProject;
        // The flat field and button styles have no disabled look of their own.
        FolderRow.Opacity = FolderRow.IsEnabled ? 1 : 0.4;
        FolderLabel.Text = isProject ? "Project" : "Login folder";
        AutomationProperties.SetName(FolderBox, FolderLabel.Text);
        BrowseButton.Visibility = isProject ? Visibility.Collapsed : Visibility.Visible;
        FolderHelp.Text = isProject
            ? "The Google Cloud project id the API key belongs to. Needs the Google Cloud SDK signed in (gcloud auth login)."
            : provider.LoginFolderVariable is { } variable
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
            NameBox.Text, SelectedProvider.Id, FolderRow.IsEnabled ? FolderBox.Text : null, home, settings.Accounts);
        if (account is null)
        {
            AddError.Text = error;
            AddError.Visibility = Visibility.Visible;
            return;
        }
        ChangeAccounts([.. settings.Accounts, account]);
        CloseAddForm();
    }
}
