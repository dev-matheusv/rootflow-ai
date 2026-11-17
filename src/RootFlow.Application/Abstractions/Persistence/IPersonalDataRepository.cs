using RootFlow.Application.Account;

namespace RootFlow.Application.Abstractions.Persistence;

public sealed record AccountDeletionBlockers(
    IReadOnlyList<string> SharedOwnedWorkspaces,
    IReadOnlyList<string> PaidSoleOwnedWorkspaces)
{
    public bool Any => SharedOwnedWorkspaces.Count > 0 || PaidSoleOwnedWorkspaces.Count > 0;
}

public interface IPersonalDataRepository
{
    Task<PersonalDataExport?> ExportAsync(Guid userId, DateTime generatedAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Why the account cannot be deleted yet: active workspaces the user owns that have other members
    /// (cannot erase other people's company), and sole-owned workspaces with a paid subscription still
    /// running (must be canceled first so nobody keeps being charged).
    /// </summary>
    Task<AccountDeletionBlockers> GetDeletionBlockersAsync(Guid userId, DateTime asOfUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the person in one transaction: their conversations (so they do not become visible to admins
    /// as unowned), hands authored training programs to the workspace owner, then removes the user row,
    /// which cascades memberships, invites sent, reset tokens, attempts and certificates.
    /// Usage events keep the billing history with the user set to null.
    /// Workspaces owned only by this person are closed in the same transaction: their documents,
    /// chunks, conversations, templates, training and pending invites are removed and the workspace is
    /// marked inactive (the row stays for billing and tax records).
    /// </summary>
    /// <returns>Storage paths of the deleted documents, for the caller to remove from disk after commit.</returns>
    Task<IReadOnlyList<string>> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
