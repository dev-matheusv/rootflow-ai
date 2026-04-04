namespace RootFlow.Application.PlatformAdmin;

public sealed class PlatformAdminOptions
{
    public List<string> Emails { get; set; } = [];

    public decimal LowCreditThresholdRatio { get; set; } = 0.40m;

    public int TrialExpiringWithinDays { get; set; } = 3;

    public int PendingPaymentAnomalyMinutes { get; set; } = 30;

    public int DashboardListSize { get; set; } = 5;

    public int ModelBreakdownSize { get; set; } = 8;
}
