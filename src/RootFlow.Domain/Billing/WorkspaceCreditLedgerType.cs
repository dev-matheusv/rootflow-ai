namespace RootFlow.Domain.Billing;

public enum WorkspaceCreditLedgerType
{
    SubscriptionGrant = 1,
    UsageDebit = 2,
    CreditPurchase = 3,
    ManualAdjustment = 4,
    Refund = 5
}
