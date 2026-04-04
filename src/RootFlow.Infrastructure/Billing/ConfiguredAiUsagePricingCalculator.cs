using Microsoft.Extensions.Options;
using RootFlow.Application.Abstractions.Billing;
using RootFlow.Application.Billing;

namespace RootFlow.Infrastructure.Billing;

public sealed class ConfiguredAiUsagePricingCalculator : IAiUsagePricingCalculator
{
    private readonly WorkspaceBillingOptions _options;

    public ConfiguredAiUsagePricingCalculator(IOptions<WorkspaceBillingOptions> options)
    {
        _options = options.Value;
    }

    public AiUsageCharge Calculate(AiUsagePricingRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Model);

        if (request.PromptTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.PromptTokens), "Prompt tokens cannot be negative.");
        }

        if (request.CompletionTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.CompletionTokens), "Completion tokens cannot be negative.");
        }

        var totalTokens = request.TotalTokens == 0
            ? checked(request.PromptTokens + request.CompletionTokens)
            : request.TotalTokens;

        if (totalTokens < request.PromptTokens + request.CompletionTokens)
        {
            throw new ArgumentOutOfRangeException(nameof(request.TotalTokens), "Total tokens cannot be less than the prompt and completion token sum.");
        }

        var rate = ResolveRate(request.Provider, request.Model);
        var estimatedCost =
            (request.PromptTokens / 1_000_000m * rate.PromptCostPerMillionTokens) +
            (request.CompletionTokens / 1_000_000m * rate.CompletionCostPerMillionTokens);

        estimatedCost = decimal.Round(estimatedCost, 6, MidpointRounding.AwayFromZero);

        var creditsCharged = estimatedCost <= 0m
            ? 0
            : (long)Math.Ceiling(estimatedCost * _options.CreditsPerDollar);

        return new AiUsageCharge(estimatedCost, creditsCharged);
    }

    private WorkspaceBillingModelRateOptions ResolveRate(string provider, string model)
    {
        var normalizedProvider = provider.Trim();
        var normalizedModel = model.Trim();

        foreach (var configuredModel in _options.Models)
        {
            if (!string.Equals(
                    configuredModel.Provider,
                    normalizedProvider,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(configuredModel.Model, normalizedModel, StringComparison.OrdinalIgnoreCase)
                || normalizedModel.StartsWith($"{configuredModel.Model}-", StringComparison.OrdinalIgnoreCase))
            {
                return configuredModel;
            }
        }

        return new WorkspaceBillingModelRateOptions
        {
            Provider = normalizedProvider,
            Model = normalizedModel,
            PromptCostPerMillionTokens = _options.DefaultPromptCostPerMillionTokens,
            CompletionCostPerMillionTokens = _options.DefaultCompletionCostPerMillionTokens
        };
    }
}
