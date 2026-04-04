using RootFlow.Domain.Billing;

namespace RootFlow.Application.Billing.Commands;

public sealed record GrantWorkspaceCreditsCommand(
    Guid WorkspaceId,
    long Credits,
    WorkspaceCreditLedgerType Type,
    string Description,
    string? ReferenceType = null,
    string? ReferenceId = null);
