using RootFlow.Domain.Billing;

namespace RootFlow.Application.Billing.Dtos;

public sealed record WorkspaceCreditLedgerEntryDto(
    Guid Id,
    Guid WorkspaceId,
    WorkspaceCreditLedgerType Type,
    long Amount,
    string Description,
    string? ReferenceType,
    string? ReferenceId,
    DateTime CreatedAtUtc);
