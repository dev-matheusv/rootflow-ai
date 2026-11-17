namespace RootFlow.Application.Billing;

/// <summary>
/// RF-10: plans and credit packs sold by contract (Enterprise). They never show in the public catalog
/// and cannot be bought by self checkout, except by the workspaces listed in configuration
/// (the customer who signed the contract). Configured under <c>Billing:SalesLed</c>, e.g.
/// <c>Billing__SalesLed__WorkspaceIds__0=&lt;workspace id&gt;</c>.
/// </summary>
public sealed class SalesLedCatalogOptions
{
    public List<string> PlanCodes { get; set; } = [];

    public List<string> CreditPackCodes { get; set; } = [];

    public List<Guid> WorkspaceIds { get; set; } = [];
}

public sealed class SalesLedCatalog
{
    // Enterprise is always sales led, even if configuration is missing: fail closed.
    private static readonly string[] AlwaysSalesLed = ["enterprise"];

    private readonly HashSet<string> _planCodes;
    private readonly HashSet<string> _creditPackCodes;
    private readonly HashSet<Guid> _workspaceIds;

    public SalesLedCatalog(SalesLedCatalogOptions options)
    {
        _planCodes = new HashSet<string>(AlwaysSalesLed.Concat(options.PlanCodes), StringComparer.OrdinalIgnoreCase);
        _creditPackCodes = new HashSet<string>(AlwaysSalesLed.Concat(options.CreditPackCodes), StringComparer.OrdinalIgnoreCase);
        _workspaceIds = [.. options.WorkspaceIds];
    }

    public bool IsPlanAvailableTo(Guid workspaceId, string? planCode)
        => planCode is null || !_planCodes.Contains(planCode.Trim()) || _workspaceIds.Contains(workspaceId);

    public bool IsCreditPackAvailableTo(Guid workspaceId, string? creditPackCode)
        => creditPackCode is null || !_creditPackCodes.Contains(creditPackCode.Trim()) || _workspaceIds.Contains(workspaceId);
}

public sealed class SalesLedOfferException : Exception
{
    public SalesLedOfferException()
        : base("This offer is available by contract only. Contact us to enable it for your workspace.")
    {
    }
}
