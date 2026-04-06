using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RootFlow.Application.Billing;

namespace RootFlow.Infrastructure.Billing;

public sealed class StripeWebhookReplayBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StripeWebhookReplayBackgroundService> _logger;

    public StripeWebhookReplayBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<StripeWebhookReplayBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ReplayOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ReplayOnceAsync(stoppingToken);
        }
    }

    private async Task ReplayOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var workspacePaymentService = scope.ServiceProvider.GetRequiredService<WorkspacePaymentService>();
            var processedCount = await workspacePaymentService.ReplayPendingStripeWebhooksAsync(
                cancellationToken: cancellationToken);

            if (processedCount > 0)
            {
                _logger.LogInformation(
                    "Replayed {ReplayCount} stored Stripe webhook events from the durable billing inbox.",
                    processedCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Stripe webhook replay background service failed during a polling cycle.");
        }
    }
}
