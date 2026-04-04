namespace RootFlow.Domain.Billing;

public sealed class BillingPlan
{
    private BillingPlan()
    {
    }

    public BillingPlan(
        Guid id,
        string code,
        string name,
        decimal monthlyPrice,
        string currencyCode,
        long includedCredits,
        int maxUsers,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Billing plan id cannot be empty.", nameof(id));
        }

        if (monthlyPrice < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(monthlyPrice), "Monthly price cannot be negative.");
        }

        if (includedCredits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(includedCredits), "Included credits cannot be negative.");
        }

        if (maxUsers <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxUsers), "Maximum users must be greater than zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);

        Id = id;
        Code = code.Trim().ToLowerInvariant();
        Name = name.Trim();
        MonthlyPrice = decimal.Round(monthlyPrice, 2, MidpointRounding.AwayFromZero);
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        IncludedCredits = includedCredits;
        MaxUsers = maxUsers;
        CreatedAtUtc = createdAtUtc;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public decimal MonthlyPrice { get; private set; }

    public string CurrencyCode { get; private set; } = null!;

    public long IncludedCredits { get; private set; }

    public int MaxUsers { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
