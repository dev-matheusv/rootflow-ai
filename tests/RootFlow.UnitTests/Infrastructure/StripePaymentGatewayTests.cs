using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using RootFlow.Application.Abstractions.Billing;
using RootFlow.Application.Billing;
using RootFlow.Infrastructure.Billing;

namespace RootFlow.UnitTests.Infrastructure;

public sealed class StripePaymentGatewayTests
{
    [Fact]
    public async Task GetSubscriptionAsync_ReadsItemLevelBillingPeriods_WhenTopLevelFieldsAreMissing()
    {
        var workspaceId = Guid.NewGuid();
        const long currentPeriodStart = 1_775_347_200;
        const long currentPeriodEnd = 1_777_939_200;
        var payload = $$"""
                        {
                          "id": "sub_item_periods",
                          "status": "active",
                          "customer": "cus_item_periods",
                          "metadata": {
                            "workspace_id": "{{workspaceId}}",
                            "billing_plan_code": "pro"
                          },
                          "items": {
                            "data": [
                              {
                                "price": {
                                  "id": "price_pro"
                                },
                                "current_period_start": {{currentPeriodStart}},
                                "current_period_end": {{currentPeriodEnd}}
                              }
                            ]
                          }
                        }
                        """;

        var gateway = CreateGateway(payload);

        var snapshot = await gateway.GetSubscriptionAsync("sub_item_periods");

        Assert.Equal("sub_item_periods", snapshot.SubscriptionId);
        Assert.Equal(workspaceId, snapshot.WorkspaceId);
        Assert.Equal("pro", snapshot.PlanCode);
        Assert.Equal("cus_item_periods", snapshot.CustomerId);
        Assert.Equal("price_pro", snapshot.PriceId);
        Assert.Equal("active", snapshot.Status);
        Assert.Equal(
            DateTimeOffset.FromUnixTimeSeconds(currentPeriodStart).UtcDateTime,
            snapshot.CurrentPeriodStartUtc);
        Assert.Equal(
            DateTimeOffset.FromUnixTimeSeconds(currentPeriodEnd).UtcDateTime,
            snapshot.CurrentPeriodEndUtc);
    }

    [Fact]
    public void ParseWebhook_SubscriptionUpdated_ReadsItemLevelBillingPeriods_WhenTopLevelFieldsAreMissing()
    {
        var workspaceId = Guid.NewGuid();
        const long currentPeriodStart = 1_775_347_200;
        const long currentPeriodEnd = 1_777_939_200;
        var webhookCreated = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $$"""
                        {
                          "id": "evt_subscription_updated",
                          "type": "customer.subscription.updated",
                          "created": {{webhookCreated}},
                          "data": {
                            "object": {
                              "id": "sub_item_periods",
                              "status": "active",
                              "customer": "cus_item_periods",
                              "metadata": {
                                "workspace_id": "{{workspaceId}}",
                                "billing_plan_code": "pro"
                              },
                              "items": {
                                "data": [
                                  {
                                    "price": {
                                      "id": "price_pro"
                                    },
                                    "current_period_start": {{currentPeriodStart}},
                                    "current_period_end": {{currentPeriodEnd}}
                                  }
                                ]
                              }
                            }
                          }
                        }
                        """;

        var gateway = CreateGateway("{}");

        var webhookEvent = Assert.IsType<StripeSubscriptionUpdatedEvent>(
            gateway.ParseWebhook(payload, CreateSignatureHeader(payload)));

        Assert.Equal("evt_subscription_updated", webhookEvent.EventId);
        Assert.Equal("customer.subscription.updated", webhookEvent.EventType);
        Assert.Equal("sub_item_periods", webhookEvent.SubscriptionId);
        Assert.Equal("cus_item_periods", webhookEvent.CustomerId);
        Assert.Equal("price_pro", webhookEvent.PriceId);
        Assert.Equal("active", webhookEvent.Status);
        Assert.Equal(workspaceId, webhookEvent.WorkspaceId);
        Assert.Equal("pro", webhookEvent.PlanCode);
        Assert.Equal(
            DateTimeOffset.FromUnixTimeSeconds(currentPeriodStart).UtcDateTime,
            webhookEvent.CurrentPeriodStartUtc);
        Assert.Equal(
            DateTimeOffset.FromUnixTimeSeconds(currentPeriodEnd).UtcDateTime,
            webhookEvent.CurrentPeriodEndUtc);
    }

    [Fact]
    public void ParseWebhook_InvoicePaymentSucceeded_UsesInvoicePaidParser()
    {
        var webhookCreated = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = $$"""
                        {
                          "id": "evt_invoice_payment_succeeded",
                          "type": "invoice.payment_succeeded",
                          "created": {{webhookCreated}},
                          "data": {
                            "object": {
                              "id": "in_payment_succeeded",
                              "subscription": "sub_payment_succeeded",
                              "customer": "cus_payment_succeeded",
                              "amount_paid": 9990,
                              "currency": "brl",
                              "lines": {
                                "data": [
                                  {
                                    "period": {
                                      "start": 1775347200,
                                      "end": 1777939200
                                    },
                                    "price": {
                                      "id": "price_pro"
                                    }
                                  }
                                ]
                              }
                            }
                          }
                        }
                        """;

        var gateway = CreateGateway("{}");

        var webhookEvent = Assert.IsType<StripeInvoicePaidEvent>(
            gateway.ParseWebhook(payload, CreateSignatureHeader(payload)));

        Assert.Equal("evt_invoice_payment_succeeded", webhookEvent.EventId);
        Assert.Equal("invoice.paid", webhookEvent.EventType);
        Assert.Equal("in_payment_succeeded", webhookEvent.InvoiceId);
        Assert.Equal("sub_payment_succeeded", webhookEvent.SubscriptionId);
        Assert.Equal("cus_payment_succeeded", webhookEvent.CustomerId);
        Assert.Equal("price_pro", webhookEvent.PriceId);
        Assert.Equal(99.90m, webhookEvent.AmountPaid);
        Assert.Equal("BRL", webhookEvent.CurrencyCode);
    }

    private static StripePaymentGateway CreateGateway(string responsePayload)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(responsePayload))
        {
            BaseAddress = new Uri("https://api.stripe.com/")
        };

        return new StripePaymentGateway(
            httpClient,
            Options.Create(new StripeBillingOptions
            {
                SecretKey = "sk_test_123",
                WebhookSecret = "whsec_test_123"
            }));
    }

    private static string CreateSignatureHeader(string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signedPayload = $"{timestamp}.{payload}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("whsec_test_123"));
        var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload))).ToLowerInvariant();

        return $"t={timestamp},v1={signature}";
    }

    private sealed class StubHttpMessageHandler(string responsePayload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responsePayload, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
