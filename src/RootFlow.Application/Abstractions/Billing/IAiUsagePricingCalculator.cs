namespace RootFlow.Application.Abstractions.Billing;

public interface IAiUsagePricingCalculator
{
    AiUsageCharge Calculate(AiUsagePricingRequest request);
}
