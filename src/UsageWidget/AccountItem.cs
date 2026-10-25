using UsageWidget.Core;

namespace UsageWidget;

/// <summary>One line of the Accounts view.</summary>
sealed class AccountItem
{
    public required int Index { get; init; }
    public required string Name { get; init; }
    public required string ProviderName { get; init; }
    public required string FolderText { get; init; }
    public required bool Shown { get; init; }

    public static AccountItem From(AccountSettings account, int index, string home) => new()
    {
        Index = index,
        Name = account.Name,
        ProviderName = AccountCatalog.Find(account.Provider)?.DisplayName ?? account.Provider,
        FolderText = AccountCatalog.Project(account) ?? AccountCatalog.DisplayFolder(AccountCatalog.LoginFolder(account, home), home),
        Shown = !account.Hidden,
    };
}
