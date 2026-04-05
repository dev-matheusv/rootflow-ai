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

    public Task EnsureTrialProvisionedAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        return EnsureProvisionedAsync(workspaceId, cancellationToken);
    }

    public async Task<WorkspaceCreditSummaryDto> GetCreditSummaryAsync(
        GetWorkspaceCreditSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Generating workspace billing summary for workspace {WorkspaceId}.",
            query.WorkspaceId);

        try
        {
            await EnsureProvisionedAsync(query.WorkspaceId, cancellationToken);

            var balance = await RequireBalanceAsync(query.WorkspaceId, cancellationToken);

            WorkspaceSubscription? subscription = null;
            try
            {
                subscription = await GetEffectiveSubscriptionAsync(query.WorkspaceId, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Workspace billing summary could not resolve the effective subscription for workspace {WorkspaceId}. Returning a degraded summary.",
                    query.WorkspaceId);
            }

            BillingPlan? plan = null;
            if (subscription is not null)
            {
                try
                {
                    plan = await _billingPlanRepository.GetByIdAsync(subscription.BillingPlanId, cancellationToken);

                    if (plan is null)
                    {
                        _logger.LogWarning(
                            "Workspace billing summary resolved subscription {SubscriptionId} for workspace {WorkspaceId}, but billing plan {BillingPlanId} was missing.",
                            subscription.Id,
                            query.WorkspaceId,
                            subscription.BillingPlanId);
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Workspace billing summary could not resolve billing plan {BillingPlanId} for workspace {WorkspaceId}. Returning a degraded subscription summary.",
                        subscription.BillingPlanId,
                        query.WorkspaceId);
                    subscription = null;
                    plan = null;
                }
            }

            var summary = new WorkspaceCreditSummaryDto(
                plan is null ? null : MapPlan(plan),
                subscription is null ? null : MapSubscription(subscription),
                MapBalance(balance));

            _logger.LogInformation(
                "Generated workspace billing summary for workspace {WorkspaceId}. Subscription status: {SubscriptionStatus}. Plan code: {PlanCode}. Available credits: {AvailableCredits}.",
                query.WorkspaceId,
                summary.Subscription?.Status,
                summary.BillingPlan?.Code,
                summary.Balance.AvailableCredits);

            return summary;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Workspace billing summary generation failed for workspace {WorkspaceId}. Returning a degraded summary.",
                query.WorkspaceId);

            return await BuildDegradedSummaryAsync(query.WorkspaceId, cancellationToken);
        }
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

    public async Task EnsureAssistantUsageAllowedAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        await EnsureProvisionedAsync(workspaceId, cancellationToken);

        var subscription = await GetEffectiveSubscriptionAsync(workspaceId, cancellationToken);
        if (subscription is null || !subscription.IsActiveAt(_clock.UtcNow))
        {
            _logger.LogWarning(
                "Blocked assistant usage for workspace {WorkspaceId} because no current billable subscription was found. Subscription status: {SubscriptionStatus}, trial ends at: {TrialEndsAtUtc}, current period end: {CurrentPeriodEndUtc}.",
                workspaceId,
                subscription?.Status,
                subscription?.TrialEndsAtUtc,
                subscription?.CurrentPeriodEndUtc);

            throw new WorkspaceSubscriptionInactiveException("Your workspace needs an active subscription.");
        }

        var balance = await RequireBalanceAsync(workspaceId, cancellationToken);
        var minimumCreditsRequired = Math.Max(1, _options.MinimumAssistantCreditsRequired);

        if (!balance.HasAvailableCredits(minimumCreditsRequired))
        {
            _logger.LogWarning(
                "Blocked assistant usage for workspace {WorkspaceId} because available credits {AvailableCredits} are below the assistant preflight requirement {RequiredCredits}.",
                workspaceId,
                balance.AvailableCredits,
                minimumCreditsRequired);

            throw new InsufficientWorkspaceCreditsException("Your workspace has no credits available.");
        }
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

        WorkspaceCreditBalance updatedBalance;
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
                updatedBalance = await _workspaceBillingRepository.RecordUsageAsync(usageEvent, ledgerEntry, cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                throw new InsufficientWorkspaceCreditsException(exception.Message);
            }
        }
        else
        {
            await _workspaceBillingRepository.AddUsageEventAsync(usageEvent, cancellationToken);
            updatedBalance = await RequireBalanceAsync(command.WorkspaceId, cancellationToken);
        }

        _logger.LogInformation(
            "Recorded assistant usage for workspace {WorkspaceId}: usage event {UsageEventId}, provider {Provider}, model {Model}, prompt tokens {PromptTokens}, completion tokens {CompletionTokens}, total tokens {TotalTokens}, real cost {EstimatedCost}, charged cost {ChargedCost}, credits charged {CreditsCharged}, remaining credits {RemainingCredits}.",
            usageEvent.WorkspaceId,
            usageEvent.Id,
            usageEvent.Provider,
            usageEvent.Model,
            usageEvent.PromptTokens,
            usageEvent.CompletionTokens,
            usageEvent.TotalTokens,
            usageEvent.EstimatedCost,
            usageCharge.ChargedCost,
            usageEvent.CreditsCharged,
            updatedBalance.AvailableCredits);

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
        var trialEndsAtUtc = utcNow.AddDays(_options.TrialPeriodDays);
        var subscription = new WorkspaceSubscription(
            Guid.NewGuid(),
            workspaceId,
            defaultPlan.Id,
            WorkspaceSubscriptionStatus.Trial,
            utcNow,
            trialEndsAtUtc,
            utcNow,
            utcNow,
            trialEndsAtUtc: trialEndsAtUtc);

        var balance = new WorkspaceCreditBalance(workspaceId, 0, 0, utcNow);
        WorkspaceCreditLedgerEntry? initialGrantEntry = _options.TrialIncludedCredits > 0
            ? new WorkspaceCreditLedgerEntry(
                Guid.NewGuid(),
                workspaceId,
                WorkspaceCreditLedgerType.SubscriptionGrant,
                _options.TrialIncludedCredits,
                $"Trial credits for the {defaultPlan.Name} plan",
                utcNow,
                "workspace_subscription",
                subscription.Id.ToString())
            : null;

        await _workspaceBillingRepository.EnsureProvisionedAsync(
            subscription,
            balance,
            initialGrantEntry,
            cancellationToken);
    }

    private async Task<WorkspaceSubscription?> GetEffectiveSubscriptionAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var utcNow = _clock.UtcNow;
        var currentSubscription = await _workspaceBillingRepository.GetCurrentSubscriptionAsync(
            workspaceId,
            utcNow,
            cancellationToken);

        if (currentSubscription is not null)
        {
            return currentSubscription;
        }

        var latestSubscription = await _workspaceBillingRepository.GetLatestSubscriptionAsync(
            workspaceId,
            cancellationToken);

        if (latestSubscription is not null && latestSubscription.ShouldExpireAt(utcNow))
        {
            latestSubscription.MarkExpired(utcNow);
            var rowsAffected = await _workspaceBillingRepository.UpdateSubscriptionAsync(latestSubscription, cancellationToken);
            if (rowsAffected == 0)
            {
                throw new InvalidOperationException(
                    $"Workspace subscription {latestSubscription.Id} could not be updated while expiring the workspace subscription state.");
            }
        }

        return latestSubscription;
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

    private async Task<WorkspaceCreditSummaryDto> BuildDegradedSummaryAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        try
        {
            await EnsureProvisionedAsync(workspaceId, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Workspace billing degraded summary could not reprovision workspace {WorkspaceId}. Falling back to a zero-balance summary.",
                workspaceId);
        }

        try
        {
            var balance = await _workspaceBillingRepository.GetCreditBalanceAsync(workspaceId, cancellationToken);
            if (balance is not null)
            {
                return new WorkspaceCreditSummaryDto(
                    null,
                    null,
                    MapBalance(balance));
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Workspace billing degraded summary could not load the credit balance for workspace {WorkspaceId}. Falling back to a zero-balance summary.",
                workspaceId);
        }

        return new WorkspaceCreditSummaryDto(
            null,
            null,
            new WorkspaceCreditBalanceDto(workspaceId, 0, 0, _clock.UtcNow));
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
            subscription.TrialEndsAtUtc,
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
