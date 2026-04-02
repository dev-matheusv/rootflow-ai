using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RootFlow.Application.Abstractions.Workspaces;
using RootFlow.Infrastructure.Configuration;
using RootFlow.Infrastructure.Workspaces;

namespace RootFlow.UnitTests.Infrastructure;

public sealed class LoggingWorkspaceInvitationNotifierTests
{
    [Fact]
    public async Task SendInviteLinkAsync_UsesConfiguredFrontendBaseUrlInDevelopmentLogs()
    {
        var logger = new TestLogger<LoggingWorkspaceInvitationNotifier>();
        var notifier = new LoggingWorkspaceInvitationNotifier(
            Options.Create(new WorkspaceInvitationOptions
            {
                FrontendBaseUrl = "https://app.rootflow.test"
            }),
            new FakeHostEnvironment("Development"),
            logger);

        await notifier.SendInviteLinkAsync(new WorkspaceInvitationNotification(
            "invitee@rootflow.test",
            "Acme Ops",
            "Jordan Rivera",
            RootFlow.Domain.Workspaces.WorkspaceRole.Admin,
            "invite-token",
            new DateTime(2026, 4, 9, 13, 0, 0, DateTimeKind.Utc)));

        Assert.Contains(
            logger.Messages,
            message => message.Contains(
                "https://app.rootflow.test/auth/invite?token=invite-token",
                StringComparison.Ordinal));
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
}
