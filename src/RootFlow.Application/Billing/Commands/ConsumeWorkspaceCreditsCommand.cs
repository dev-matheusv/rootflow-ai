using RootFlow.Domain.Billing;

namespace RootFlow.Application.Billing.Commands;

public sealed record ConsumeWorkspaceCreditsCommand(
    Guid WorkspaceId,
    long Credits,
    WorkspaceCreditLedgerType Type,
    string Description,
    string? ReferenceType = null,
    string? ReferenceId = null);
