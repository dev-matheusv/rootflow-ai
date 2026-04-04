using Microsoft.Extensions.Options;
using RootFlow.Application.PlatformAdmin;
using RootFlow.Infrastructure.Auth;

namespace RootFlow.UnitTests.Auth;

public sealed class ConfiguredPlatformAdminAccessServiceTests
{
    [Fact]
    public void HasAccess_ReturnsTrue_ForConfiguredEmail_IgnoringCaseAndWhitespace()
    {
        var service = new ConfiguredPlatformAdminAccessService(
            Options.Create(new PlatformAdminOptions
            {
                Emails = [" owner@rootflow.test "]
            }));

        Assert.True(service.HasAccess("OWNER@rootflow.test"));
    }

    [Fact]
    public void HasAccess_ReturnsFalse_ForUnknownEmail()
    {
        var service = new ConfiguredPlatformAdminAccessService(
            Options.Create(new PlatformAdminOptions
            {
                Emails = ["owner@rootflow.test"]
            }));

        Assert.False(service.HasAccess("member@rootflow.test"));
    }
}
