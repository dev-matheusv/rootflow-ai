namespace RootFlow.Application.Billing;

public sealed class WorkspaceBillingModelRateOptions
{
    public string Provider { get; set; } = "openai";

    public string Model { get; set; } = string.Empty;

    public decimal PromptCostPerMillionTokens { get; set; }

    public decimal CompletionCostPerMillionTokens { get; set; }
}
