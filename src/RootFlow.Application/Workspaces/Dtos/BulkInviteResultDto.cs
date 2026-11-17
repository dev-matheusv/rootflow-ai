namespace RootFlow.Application.Workspaces.Dtos;

/// <summary>Status: sent, already_member, invalid_email, duplicate, failed.</summary>
public sealed record BulkInviteItemDto(string Email, string Status, string? Message);

public sealed record BulkInviteResultDto(int Sent, int Skipped, IReadOnlyList<BulkInviteItemDto> Items);
