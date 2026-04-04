namespace RootFlow.Application.Billing;

public sealed class StripeBillingOptions
{
    public string SecretKey { get; set; } = string.Empty;

    public string WebhookSecret { get; set; } = string.Empty;

    public int WebhookToleranceSeconds { get; set; } = 300;

    public string CheckoutSuccessPath { get; set; } = "/billing?checkout=success";

    public string CheckoutCancelPath { get; set; } = "/billing?checkout=cancel";

    public List<StripePlanPriceOptions> PlanPrices { get; set; } = [];

    public List<StripeCreditPackOptions> CreditPacks { get; set; } = [];
}

public sealed class StripePlanPriceOptions
{
    public string PlanCode { get; set; } = string.Empty;

    public string PriceId { get; set; } = string.Empty;
}

public sealed class StripeCreditPackOptions
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public long Credits { get; set; }

    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; } = "BRL";

    public string PriceId { get; set; } = string.Empty;
}
