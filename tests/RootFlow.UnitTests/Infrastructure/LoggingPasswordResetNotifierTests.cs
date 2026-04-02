using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.FileProviders;
using RootFlow.Application.Abstractions.Auth;
using RootFlow.Infrastructure.Auth;
using RootFlow.Infrastructure.Configuration;
using RootFlow.Infrastructure.Email;

namespace RootFlow.UnitTests.Infrastructure;

public sealed class LoggingPasswordResetNotifierTests
{
    [Fact]
    public async Task SendResetLinkAsync_UsesConfiguredFrontendBaseUrlInDevelopmentLogs()
    {
        var environment = new FakeHostEnvironment("Development");
        var logger = new TestLogger<LoggingPasswordResetNotifier>();
        var notifier = new LoggingPasswordResetNotifier(
            new DisabledEmailSender(),
            new RootFlowAppLinkBuilder(
                Options.Create(new PasswordResetOptions
                {
                    FrontendBaseUrl = "https://app.rootflow.test"
                }),
                Options.Create(new WorkspaceInvitationOptions()),
                environment),
            environment,
            logger);

        await notifier.SendResetLinkAsync(new PasswordResetNotification(
            "jordan@rootflow.test",
            "Jordan Rivera",
            "reset-token",
            new DateTime(2026, 4, 2, 13, 0, 0, DateTimeKind.Utc)));

        Assert.Contains(
            logger.Messages,
            message => message.Contains(
                "https://app.rootflow.test/auth/reset-password?token=reset-token",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendResetLinkAsync_SendsEmailWhenOutboundDeliveryIsConfigured()
    {
        var environment = new FakeHostEnvironment("Production");
        var logger = new TestLogger<LoggingPasswordResetNotifier>();
        var emailSender = new CapturingEmailSender();
        var notifier = new LoggingPasswordResetNotifier(
            emailSender,
            new RootFlowAppLinkBuilder(
                Options.Create(new PasswordResetOptions
                {
                    FrontendBaseUrl = "https://app.rootflow.test"
                }),
                Options.Create(new WorkspaceInvitationOptions()),
                environment),
            environment,
            logger);

        await notifier.SendResetLinkAsync(new PasswordResetNotification(
            "jordan@rootflow.test",
            "Jordan Rivera",
            "reset-token",
            new DateTime(2026, 4, 2, 13, 0, 0, DateTimeKind.Utc)));

        var message = Assert.Single(emailSender.Messages);
        Assert.Equal("Reset your RootFlow password", message.Subject);
        Assert.Contains("https://app.rootflow.test/auth/reset-password?token=reset-token", message.HtmlBody, StringComparison.Ordinal);
        Assert.Empty(logger.Messages);
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "RootFlow.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return NoopDisposable.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NoopDisposable : IDisposable
        {
            public static NoopDisposable Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed class DisabledEmailSender : IEmailSender
    {
        public bool IsConfigured => false;

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Email delivery should not be attempted when the sender is disabled.");
        }
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = [];

        public bool IsConfigured => true;

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
