namespace RootFlow.Application.Billing;

public sealed class WorkspaceBillingOptions
{
    public string DefaultPlanCode { get; set; } = "starter";

    public int DefaultSubscriptionPeriodDays { get; set; } = 30;

    public int TrialPeriodDays { get; set; } = 7;

    public long TrialIncludedCredits { get; set; } = 5_000;

    public long MinimumAssistantCreditsRequired { get; set; } = 1;

    public long CreditsPerDollar { get; set; } = 100;

    public decimal UsageMarkupMultiplier { get; set; } = 2.0m;

    public decimal DefaultPromptCostPerMillionTokens { get; set; } = 0.40m;

    public decimal DefaultCompletionCostPerMillionTokens { get; set; } = 1.60m;

    public List<WorkspaceBillingModelRateOptions> Models { get; set; } = [];
}
