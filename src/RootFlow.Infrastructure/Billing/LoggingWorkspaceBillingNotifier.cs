using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RootFlow.Application.Abstractions.Auth;
using RootFlow.Application.Abstractions.Billing;
using RootFlow.Infrastructure.Email;

namespace RootFlow.Infrastructure.Billing;

public sealed class LoggingWorkspaceBillingNotifier : IWorkspaceBillingNotifier
{
    private readonly IEmailSender _emailSender;
    private readonly IAppLinkBuilder _appLinkBuilder;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<LoggingWorkspaceBillingNotifier> _logger;

    public LoggingWorkspaceBillingNotifier(
        IEmailSender emailSender,
        IAppLinkBuilder appLinkBuilder,
        IHostEnvironment hostEnvironment,
        ILogger<LoggingWorkspaceBillingNotifier> logger)
    {
        _emailSender = emailSender;
        _appLinkBuilder = appLinkBuilder;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task SendPaymentConfirmedAsync(
        WorkspacePaymentConfirmationNotification notification,
        CancellationToken cancellationToken = default)
    {
        var billingLink = _appLinkBuilder.BuildAppRouteLink("/billing", requireAbsoluteUrl: _emailSender.IsConfigured);
        var template = BuildTemplate(notification, billingLink);

        if (_emailSender.IsConfigured)
        {
            await _emailSender.SendAsync(
                RootFlowEmailTemplate.CreateMessage(notification.Email, notification.FullName, template),
                cancellationToken);

            _logger.LogInformation(
                "Sent RootFlow billing confirmation email to {Email} for workspace {WorkspaceName}, kind {PaymentKind}, item {ItemName}.",
                notification.Email,
                notification.WorkspaceName,
                notification.Kind,
                notification.ItemName);
            return;
        }

        if (_hostEnvironment.IsDevelopment() || _hostEnvironment.IsEnvironment("IntegrationTesting"))
        {
            _logger.LogInformation(
                "Billing confirmation email requested for {Email}. Workspace: {WorkspaceName}. Kind: {PaymentKind}. Item: {ItemName}. Amount: {AmountPaid} {CurrencyCode}.",
                notification.Email,
                notification.WorkspaceName,
                notification.Kind,
                notification.ItemName,
                notification.AmountPaid,
                notification.CurrencyCode);
            return;
        }

        _logger.LogWarning(
            "Billing confirmation email for {Email} was skipped because outbound email is not configured.",
            notification.Email);
        return;
#if false
        if (_emailSender.IsConfigured)
        {
            await _emailSender.SendAsync(
                RootFlowEmailTemplate.CreateMessage(
                    notification.Email,
                    notification.FullName,
                    new ActionEmailTemplate(
                        "Pagamento confirmado - RootFlow",
                        $"Seu plano {notification.PlanName} foi confirmado com sucesso.",
                        "Pagamento confirmado",
                        "Seu pagamento foi confirmado",
                        $"O workspace {notification.WorkspaceName} agora está com o plano {notification.PlanName} ativo na RootFlow.",
                        "Ver faturamento",
                        billingLink,
                        [
                            $"Plano: {notification.PlanName}",
                            $"Valor pago: {FormatCurrency(notification.AmountPaid, notification.CurrencyCode)}",
                            "A assinatura já está disponível para o workspace."
                        ],
                        "Se você precisar acompanhar créditos, plano atual ou próximas cobranças, acesse a área de faturamento.",
                        "Esta é uma confirmação automática de pagamento da RootFlow.")),
                cancellationToken);

            _logger.LogInformation(
                "Sent RootFlow billing confirmation email to {Email} for workspace {WorkspaceName} and plan {PlanName}.",
                notification.Email,
                notification.WorkspaceName,
                notification.PlanName);
            return;
        }

        if (_hostEnvironment.IsDevelopment() || _hostEnvironment.IsEnvironment("IntegrationTesting"))
        {
            _logger.LogInformation(
                "Billing confirmation email requested for {Email}. Workspace: {WorkspaceName}. Plan: {PlanName}. Amount: {AmountPaid} {CurrencyCode}.",
                notification.Email,
                notification.WorkspaceName,
                notification.PlanName,
                notification.AmountPaid,
                notification.CurrencyCode);
            return;
        }

        _logger.LogWarning(
            "Billing confirmation email for {Email} was skipped because outbound email is not configured.",
            notification.Email);
#endif
    }

    public async Task SendLifecycleNotificationAsync(
        WorkspaceBillingLifecycleNotification notification,
        CancellationToken cancellationToken = default)
    {
        var billingLink = _appLinkBuilder.BuildAppRouteLink("/billing", requireAbsoluteUrl: _emailSender.IsConfigured);
        var template = BuildLifecycleTemplate(notification, billingLink);

        if (_emailSender.IsConfigured)
        {
            await _emailSender.SendAsync(
                RootFlowEmailTemplate.CreateMessage(notification.Email, notification.FullName, template),
                cancellationToken);

            _logger.LogInformation(
                "Sent RootFlow billing lifecycle email to {Email} for workspace {WorkspaceName}. Kind {LifecycleKind}.",
                notification.Email,
                notification.WorkspaceName,
                notification.Kind);
            return;
        }

        if (_hostEnvironment.IsDevelopment() || _hostEnvironment.IsEnvironment("IntegrationTesting"))
        {
            _logger.LogInformation(
                "Billing lifecycle email requested for {Email}. Workspace: {WorkspaceName}. Kind: {LifecycleKind}.",
                notification.Email,
                notification.WorkspaceName,
                notification.Kind);
            return;
        }

        _logger.LogWarning(
            "Billing lifecycle email for {Email} was skipped because outbound email is not configured.",
            notification.Email);
    }

    public async Task SendPlatformAlertAsync(
        PlatformBillingAlertNotification notification,
        CancellationToken cancellationToken = default)
    {
        var adminLink = _appLinkBuilder.BuildAppRouteLink("/admin", requireAbsoluteUrl: _emailSender.IsConfigured);
        var template = BuildPlatformAlertTemplate(notification, adminLink);

        if (_emailSender.IsConfigured)
        {
            await _emailSender.SendAsync(
                RootFlowEmailTemplate.CreateMessage(notification.Email, notification.FullName, template),
                cancellationToken);

            _logger.LogInformation(
                "Sent RootFlow platform billing alert email to {Email}. Payment issues {PaymentIssueCount}. Replayable webhooks {ReplayableWebhookCount}.",
                notification.Email,
                notification.PaymentIssueCount,
                notification.ReplayableWebhookCount);
            return;
        }

        if (_hostEnvironment.IsDevelopment() || _hostEnvironment.IsEnvironment("IntegrationTesting"))
        {
            _logger.LogInformation(
                "Platform billing alert email requested for {Email}. Payment issues: {PaymentIssueCount}. Replayable webhooks: {ReplayableWebhookCount}.",
                notification.Email,
                notification.PaymentIssueCount,
                notification.ReplayableWebhookCount);
            return;
        }

        _logger.LogWarning(
            "Platform billing alert email for {Email} was skipped because outbound email is not configured.",
            notification.Email);
    }

    private static ActionEmailTemplate BuildTemplate(
        WorkspacePaymentConfirmationNotification notification,
        string billingLink)
    {
        var detailLines = new List<string>
        {
            $"Item: {notification.ItemName}",
            $"Valor pago: {FormatCurrency(notification.AmountPaid, notification.CurrencyCode)}",
            notification.ConfirmationMessage
        };

        if (notification.Kind == WorkspacePaymentConfirmationKind.CreditPurchase &&
            notification.CreditsGranted is > 0)
        {
            detailLines.Insert(1, $"Creditos adicionados: {notification.CreditsGranted.GetValueOrDefault():N0}");
        }

        return notification.Kind switch
        {
            WorkspacePaymentConfirmationKind.CreditPurchase => new ActionEmailTemplate(
                "Compra confirmada - RootFlow",
                $"A compra de {notification.ItemName} foi confirmada com sucesso.",
                "Pagamento confirmado",
                "Seus creditos foram adicionados",
                $"O workspace {notification.WorkspaceName} recebeu a confirmacao de pagamento da compra de creditos.",
                "Ver faturamento",
                billingLink,
                detailLines,
                "Abra a area de faturamento para acompanhar saldo, creditos consumidos e o historico mais recente.",
                "Esta e uma confirmacao automatica de pagamento da RootFlow."),
            _ => new ActionEmailTemplate(
                "Pagamento confirmado - RootFlow",
                $"O plano {notification.ItemName} foi confirmado com sucesso.",
                "Pagamento confirmado",
                "Seu pagamento foi confirmado",
                $"O workspace {notification.WorkspaceName} agora esta com o pagamento do plano confirmado na RootFlow.",
                "Ver faturamento",
                billingLink,
                detailLines,
                "Abra a area de faturamento para acompanhar plano atual, creditos e proximas cobrancas.",
                "Esta e uma confirmacao automatica de pagamento da RootFlow.")
        };
    }

    private static string FormatCurrency(decimal amount, string currencyCode)
    {
        var normalizedCurrencyCode = currencyCode.Trim().ToUpperInvariant();
        if (string.Equals(normalizedCurrencyCode, "BRL", StringComparison.Ordinal))
        {
            return string.Format(CultureInfo.GetCultureInfo("pt-BR"), "{0:C}", amount);
        }

        return $"{normalizedCurrencyCode} {amount:N2}";
    }

    private static ActionEmailTemplate BuildLifecycleTemplate(
        WorkspaceBillingLifecycleNotification notification,
        string billingLink)
    {
        var detailLines = new List<string>();
        if (!string.IsNullOrWhiteSpace(notification.PlanName))
        {
            detailLines.Add($"Plano atual: {notification.PlanName}");
        }

        if (notification.Kind == WorkspaceBillingLifecycleNotificationKind.TrialExpiring)
        {
            if (notification.TrialDaysRemaining is not null)
            {
                detailLines.Add(
                    notification.TrialDaysRemaining <= 0
                        ? "O trial termina hoje."
                        : $"Dias restantes de trial: {notification.TrialDaysRemaining}");
            }

            if (notification.TrialEndsAtUtc.HasValue)
            {
                detailLines.Add($"Trial termina em: {notification.TrialEndsAtUtc.Value:dd/MM/yyyy HH:mm} UTC");
            }

            return new ActionEmailTemplate(
                "Trial proximo do fim - RootFlow",
                $"O trial do workspace {notification.WorkspaceName} esta perto do fim.",
                "Billing lifecycle",
                "Seu trial esta acabando",
                $"O workspace {notification.WorkspaceName} esta se aproximando do fim do periodo de trial. Este e o melhor momento para ativar um plano e evitar interrupcoes.",
                "Abrir faturamento",
                billingLink,
                detailLines,
                "Abra o faturamento para escolher o plano ideal e manter o workspace ativo.",
                "Este e um aviso automatico da RootFlow.");
        }

        if (notification.RemainingPercent is not null)
        {
            detailLines.Add($"Percentual restante: {notification.RemainingPercent}%");
        }

        detailLines.Add($"Creditos disponiveis: {notification.AvailableCredits:N0}");

        return notification.Kind switch
        {
            WorkspaceBillingLifecycleNotificationKind.NoCredits => new ActionEmailTemplate(
                "Workspace sem creditos - RootFlow",
                $"O workspace {notification.WorkspaceName} esta sem creditos.",
                "Billing lifecycle",
                "Seu workspace esta sem creditos",
                $"O workspace {notification.WorkspaceName} esgotou os creditos disponiveis. O assistente pode ficar bloqueado ate a compra de novos creditos ou upgrade de plano.",
                "Abrir faturamento",
                billingLink,
                detailLines,
                "Abra o faturamento para comprar creditos extras ou fazer upgrade do plano.",
                "Este e um aviso automatico da RootFlow."),
            WorkspaceBillingLifecycleNotificationKind.CriticalCredits => new ActionEmailTemplate(
                "Creditos criticamente baixos - RootFlow",
                $"O workspace {notification.WorkspaceName} esta com creditos criticamente baixos.",
                "Billing lifecycle",
                "Seu workspace esta ficando sem creditos",
                $"O workspace {notification.WorkspaceName} entrou em faixa critica de creditos restantes. Vale agir agora para evitar interrupcoes no uso.",
                "Abrir faturamento",
                billingLink,
                detailLines,
                "Abra o faturamento para reforcar a capacidade do workspace antes de atingir zero.",
                "Este e um aviso automatico da RootFlow."),
            _ => new ActionEmailTemplate(
                "Creditos baixos - RootFlow",
                $"O workspace {notification.WorkspaceName} esta com creditos baixos.",
                "Billing lifecycle",
                "Seu workspace esta com creditos baixos",
                $"O workspace {notification.WorkspaceName} entrou em faixa de atencao de creditos. Vale planejar a proxima recarga ou upgrade.",
                "Abrir faturamento",
                billingLink,
                detailLines,
                "Abra o faturamento para acompanhar o consumo e decidir o proximo passo.",
                "Este e um aviso automatico da RootFlow.")
        };
    }

    private static ActionEmailTemplate BuildPlatformAlertTemplate(
        PlatformBillingAlertNotification notification,
        string adminLink)
    {
        var detailLines = new List<string>
        {
            $"Problemas de pagamento: {notification.PaymentIssueCount}",
            $"Webhooks Stripe para replay: {notification.ReplayableWebhookCount}"
        };

        detailLines.AddRange(notification.DetailLines.Where(line => !string.IsNullOrWhiteSpace(line)));

        return new ActionEmailTemplate(
            "Alerta operacional de billing - RootFlow",
            "A RootFlow detectou alertas operacionais no billing.",
            "Platform ops",
            "Billing precisa de atencao",
            "A plataforma identificou anomalias em pagamentos ou sincronizacao de webhooks Stripe. Revise o painel admin para agir rapido.",
            "Abrir admin",
            adminLink,
            detailLines,
            "Use o painel admin para revisar issues recentes, acompanhar o estado de faturamento e executar replay quando necessario.",
            "Este e um alerta automatico operacional da RootFlow.");
    }
}
