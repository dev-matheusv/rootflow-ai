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
}
