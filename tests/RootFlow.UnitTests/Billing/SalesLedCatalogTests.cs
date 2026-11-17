using RootFlow.Application.Billing;

namespace RootFlow.UnitTests.Billing;

public sealed class SalesLedCatalogTests
{
    private static readonly Guid ContractWorkspace = Guid.NewGuid();
    private static readonly Guid OtherWorkspace = Guid.NewGuid();

    [Fact]
    public void Enterprise_IsHidden_EvenWithoutConfiguration()
    {
        var catalog = new SalesLedCatalog(new SalesLedCatalogOptions());

        Assert.False(catalog.IsPlanAvailableTo(OtherWorkspace, "enterprise"));
        Assert.False(catalog.IsPlanAvailableTo(OtherWorkspace, " Enterprise "));
        Assert.False(catalog.IsCreditPackAvailableTo(OtherWorkspace, "enterprise"));
    }

    [Fact]
    public void Enterprise_IsAvailable_OnlyToContractWorkspace()
    {
        var catalog = new SalesLedCatalog(new SalesLedCatalogOptions { WorkspaceIds = [ContractWorkspace] });

        Assert.True(catalog.IsPlanAvailableTo(ContractWorkspace, "enterprise"));
        Assert.True(catalog.IsCreditPackAvailableTo(ContractWorkspace, "enterprise"));
        Assert.False(catalog.IsPlanAvailableTo(OtherWorkspace, "enterprise"));
    }

    [Theory]
    [InlineData("starter")]
    [InlineData("pro")]
    [InlineData("business")]
    public void PublicPlans_StayAvailableToEveryone(string planCode)
    {
        var catalog = new SalesLedCatalog(new SalesLedCatalogOptions());

        Assert.True(catalog.IsPlanAvailableTo(OtherWorkspace, planCode));
    }

    [Fact]
    public void ConfiguredCodes_AreAlsoSalesLed()
    {
        var catalog = new SalesLedCatalog(new SalesLedCatalogOptions { PlanCodes = ["business"] });

        Assert.False(catalog.IsPlanAvailableTo(OtherWorkspace, "business"));
    }
}
