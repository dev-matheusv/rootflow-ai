namespace RootFlow.Api.Contracts.Billing;

public sealed record BillingPortalSessionResponse(
    string SessionId,
    string PortalUrl);
