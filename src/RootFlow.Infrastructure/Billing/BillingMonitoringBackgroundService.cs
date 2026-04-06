using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RootFlow.Application.Billing;

namespace RootFlow.Infrastructure.Billing;

public sealed class BillingMonitoringBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<BillingMonitoringBackgroundService> _logger;

    public BillingMonitoringBackgroundService(
        IServiceScopeFactory scopeFactory,
        IHostEnvironment hostEnvironment,
        ILogger<BillingMonitoringBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_hostEnvironment.IsEnvironment("IntegrationTesting"))
        {
            _logger.LogInformation("Billing monitoring background service is disabled in IntegrationTesting.");
            return;
        }

        await RunOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var monitoringService = scope.ServiceProvider.GetRequiredService<BillingMonitoringService>();
            var result = await monitoringService.RunAsync(cancellationToken);

            if (result.AdminAlertsSent > 0 || result.WorkspaceNotificationsSent > 0)
            {
                _logger.LogInformation(
                    "Billing monitoring sent {AdminAlertsSent} admin alerts and {WorkspaceNotificationsSent} workspace lifecycle notifications.",
                    result.AdminAlertsSent,
                    result.WorkspaceNotificationsSent);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Billing monitoring background service failed during a polling cycle.");
        }
    }
}
