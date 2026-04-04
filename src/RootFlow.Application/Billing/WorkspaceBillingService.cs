using Microsoft.Extensions.Logging;
using RootFlow.Application.Abstractions.Billing;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Billing.Commands;
using RootFlow.Application.Billing.Dtos;
using RootFlow.Application.Billing.Queries;
using RootFlow.Domain.Billing;

namespace RootFlow.Application.Billing;

public sealed class WorkspaceBillingService
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IBillingPlanRepository _billingPlanRepository;
    private readonly IWorkspaceBillingRepository _workspaceBillingRepository;
    private readonly IAiUsagePricingCalculator _usagePricingCalculator;
    private readonly IClock _clock;
    private readonly WorkspaceBillingOptions _options;
    private readonly ILogger<WorkspaceBillingService> _logger;

    public WorkspaceBillingService(
        IWorkspaceRepository workspaceRepository,
        IBillingPlanRepository billingPlanRepository,
        IWorkspaceBillingRepository workspaceBillingRepository,
        IAiUsagePricingCalculator usagePricingCalculator,
        IClock clock,
        WorkspaceBillingOptions options,
        ILogger<WorkspaceBillingService> logger)
    {
        _workspaceRepository = workspaceRepository;
        _billingPlanRepository = billingPlanRepository;
        _workspaceBillingRepository = workspaceBillingRepository;
        _usagePricingCalculator = usagePricingCalculator;
        _clock = clock;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BillingPlanDto>> ListPlansAsync(
        ListBillingPlansQuery query,
        CancellationToken cancellationToken = default)
    {
        var plans = query.ActiveOnly
            ? await _billingPlanRepository.ListActiveAsync(cancellationToken)
            : await _billingPlanRepository.ListAllAsync(cancellationToken);

        return plans.Select(MapPlan).ToArray();
    }

    public async Task<WorkspaceCreditSummaryDto> GetCreditSummaryAsync(
        GetWorkspaceCreditSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        await EnsureProvisionedAsync(query.WorkspaceId, cancellationToken);

        var subscription = await _workspaceBillingRepository.GetCurrentSubscriptionAsync(
            query.WorkspaceId,
            _clock.UtcNow,
            cancellationToken);

        subscription ??= await _workspaceBillingRepository.GetLatestSubscriptionAsync(
            query.WorkspaceId,
            cancellationToken);

        var balance = await RequireBalanceAsync(query.WorkspaceId, cancellationToken);
        var plan = subscription is null
            ? null
            : await _billingPlanRepository.GetByIdAsync(subscription.BillingPlanId, cancellationToken);

        return new WorkspaceCreditSummaryDto(
            plan is null ? null : MapPlan(plan),
            subscription is null ? null : MapSubscription(subscription),
            MapBalance(balance));
    }

    public async Task<bool> HasEnoughCreditsAsync(
        Guid workspaceId,
        long requiredCredits,
        CancellationToken cancellationToken = default)
    {
        if (requiredCredits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requiredCredits), "Required credits cannot be negative.");
        }

        await EnsureProvisionedAsync(workspaceId, cancellationToken);
        var balance = await RequireBalanceAsync(workspaceId, cancellationToken);
        return balance.HasAvailableCredits(requiredCredits);
    }

    public async Task<WorkspaceCreditSummaryDto> GrantCreditsAsync(
        GrantWorkspaceCreditsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Credits <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(command.Credits), "Granted credits must be greater than zero.");
        }

        await EnsureProvisionedAsync(command.WorkspaceId, cancellationToken);

        var entry = new WorkspaceCreditLedgerEntry(
            Guid.NewGuid(),
            command.WorkspaceId,
            command.Type,
            command.Credits,
            command.Description,
            _clock.UtcNow,
            command.ReferenceType,
            command.ReferenceId);

        await _workspaceBillingRepository.AppendLedgerEntryAsync(entry, cancellationToken);
        _logger.LogInformation(
            "Granted {Credits} credits to workspace {WorkspaceId} with ledger type {LedgerType}.",
            command.Credits,
            command.WorkspaceId,
            command.Type);

        return await GetCreditSummaryAsync(new GetWorkspaceCreditSummaryQuery(command.WorkspaceId), cancellationToken);
    }

    public async Task<WorkspaceCreditSummaryDto> ConsumeCreditsAsync(
        ConsumeWorkspaceCreditsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Credits <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(command.Credits), "Consumed credits must be greater than zero.");
        }

        await EnsureProvisionedAsync(command.WorkspaceId, cancellationToken);
        var balance = await RequireBalanceAsync(command.WorkspaceId, cancellationToken);
        if (!balance.HasAvailableCredits(command.Credits))
        {
            throw new InsufficientWorkspaceCreditsException("Workspace does not have enough credits to complete this operation.");
        }

        var entry = new WorkspaceCreditLedgerEntry(
            Guid.NewGuid(),
            command.WorkspaceId,
            command.Type,
            -command.Credits,
            command.Description,
            _clock.UtcNow,
            command.ReferenceType,
            command.ReferenceId);

        await _workspaceBillingRepository.AppendLedgerEntryAsync(entry, cancellationToken);
        _logger.LogInformation(
            "Consumed {Credits} credits from workspace {WorkspaceId} with ledger type {LedgerType}.",
            command.Credits,
            command.WorkspaceId,
            command.Type);

        return await GetCreditSummaryAsync(new GetWorkspaceCreditSummaryQuery(command.WorkspaceId), cancellationToken);
    }

    public async Task<WorkspaceUsageEventDto> RegisterUsageAsync(
        RegisterWorkspaceUsageCommand command,
        CancellationToken cancellationToken = default)
    {
        await EnsureProvisionedAsync(command.WorkspaceId, cancellationToken);

        var usageCharge = _usagePricingCalculator.Calculate(
            new AiUsagePricingRequest(
                command.Provider,
                command.Model,
                command.PromptTokens,
                command.CompletionTokens,
                command.TotalTokens));

        var createdAtUtc = _clock.UtcNow;
        var usageEvent = new WorkspaceUsageEvent(
            Guid.NewGuid(),
            command.WorkspaceId,
            command.UserId,
            command.ConversationId,
            command.Provider,
            command.Model,
            command.PromptTokens,
            command.CompletionTokens,
            command.TotalTokens,
            usageCharge.EstimatedCost,
            usageCharge.CreditsCharged,
            createdAtUtc);

        if (usageEvent.CreditsCharged > 0)
        {
            var balance = await RequireBalanceAsync(command.WorkspaceId, cancellationToken);
            if (!balance.HasAvailableCredits(usageEvent.CreditsCharged))
            {
                throw new InsufficientWorkspaceCreditsException("Workspace does not have enough credits to register this usage.");
            }

            var ledgerEntry = new WorkspaceCreditLedgerEntry(
                Guid.NewGuid(),
                command.WorkspaceId,
                WorkspaceCreditLedgerType.UsageDebit,
                -usageEvent.CreditsCharged,
                $"AI usage charge for {usageEvent.Provider}/{usageEvent.Model}",
                createdAtUtc,
                "workspace_usage_event",
                usageEvent.Id.ToString());

            try
            {
                await _workspaceBillingRepository.RecordUsageAsync(usageEvent, ledgerEntry, cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                throw new InsufficientWorkspaceCreditsException(exception.Message);
            }
        }
        else
        {
            await _workspaceBillingRepository.AddUsageEventAsync(usageEvent, cancellationToken);
        }

        _logger.LogInformation(
            "Registered usage event {UsageEventId} for workspace {WorkspaceId}: provider {Provider}, model {Model}, tokens {TotalTokens}, estimated cost {EstimatedCost}, credits charged {CreditsCharged}.",
            usageEvent.Id,
            usageEvent.WorkspaceId,
            usageEvent.Provider,
            usageEvent.Model,
            usageEvent.TotalTokens,
            usageEvent.EstimatedCost,
            usageEvent.CreditsCharged);

        return MapUsage(usageEvent);
    }

    private async Task EnsureProvisionedAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var workspaceExists = await _workspaceRepository.ExistsAsync(workspaceId, cancellationToken);
        if (!workspaceExists)
        {
            throw new InvalidOperationException("Workspace was not found.");
        }

        var defaultPlan = await _billingPlanRepository.GetByCodeAsync(
            _options.DefaultPlanCode,
            cancellationToken);

        if (defaultPlan is null)
        {
            throw new InvalidOperationException($"Billing plan '{_options.DefaultPlanCode}' is not available.");
        }

        var utcNow = _clock.UtcNow;
        var subscription = new WorkspaceSubscription(
            Guid.NewGuid(),
            workspaceId,
            defaultPlan.Id,
            WorkspaceSubscriptionStatus.Active,
            utcNow,
            utcNow.AddDays(_options.DefaultSubscriptionPeriodDays),
            utcNow,
            utcNow);

        var balance = new WorkspaceCreditBalance(workspaceId, 0, 0, utcNow);
        var initialGrantEntry = new WorkspaceCreditLedgerEntry(
            Guid.NewGuid(),
            workspaceId,
            WorkspaceCreditLedgerType.SubscriptionGrant,
            defaultPlan.IncludedCredits,
            $"Included credits for the {defaultPlan.Name} plan",
            utcNow,
            "workspace_subscription",
            subscription.Id.ToString());

        await _workspaceBillingRepository.EnsureProvisionedAsync(
            subscription,
            balance,
            initialGrantEntry,
            cancellationToken);
    }

    private async Task<WorkspaceCreditBalance> RequireBalanceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var balance = await _workspaceBillingRepository.GetCreditBalanceAsync(workspaceId, cancellationToken);
        if (balance is null)
        {
            throw new InvalidOperationException("Workspace billing balance was not provisioned.");
        }

        return balance;
    }

    private static BillingPlanDto MapPlan(BillingPlan plan)
    {
        return new BillingPlanDto(
            plan.Id,
            plan.Code,
            plan.Name,
            plan.MonthlyPrice,
            plan.CurrencyCode,
            plan.IncludedCredits,
            plan.MaxUsers,
            plan.IsActive);
    }

    private static WorkspaceSubscriptionDto MapSubscription(WorkspaceSubscription subscription)
    {
        return new WorkspaceSubscriptionDto(
            subscription.Id,
            subscription.WorkspaceId,
            subscription.BillingPlanId,
            subscription.Status,
            subscription.CurrentPeriodStartUtc,
            subscription.CurrentPeriodEndUtc,
            subscription.CanceledAtUtc,
            subscription.CreatedAtUtc,
            subscription.UpdatedAtUtc);
    }

    private static WorkspaceCreditBalanceDto MapBalance(WorkspaceCreditBalance balance)
    {
        return new WorkspaceCreditBalanceDto(
            balance.WorkspaceId,
            balance.AvailableCredits,
            balance.ConsumedCredits,
            balance.UpdatedAtUtc);
    }

    private static WorkspaceUsageEventDto MapUsage(WorkspaceUsageEvent usageEvent)
    {
        return new WorkspaceUsageEventDto(
            usageEvent.Id,
            usageEvent.WorkspaceId,
            usageEvent.UserId,
            usageEvent.ConversationId,
            usageEvent.Provider,
            usageEvent.Model,
            usageEvent.PromptTokens,
            usageEvent.CompletionTokens,
            usageEvent.TotalTokens,
            usageEvent.EstimatedCost,
            usageEvent.CreditsCharged,
            usageEvent.CreatedAtUtc);
    }
}
