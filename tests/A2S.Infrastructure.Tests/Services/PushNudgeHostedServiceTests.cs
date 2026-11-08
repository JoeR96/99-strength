using A2S.Application.Push;
using A2S.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace A2S.Infrastructure.Tests.Services;

public class PushNudgeHostedServiceTests
{
    private static readonly PushOptions Configured = new() { VapidPublicKey = "pub", VapidPrivateKey = "priv", Subject = "mailto:t@example.com" };

    private static PushNudgeHostedService Build(string environment, PushOptions options)
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environment);
        var scopes = Substitute.For<IServiceScopeFactory>();
        return new PushNudgeHostedService(scopes, env, Options.Create(options), TimeProvider.System, NullLogger<PushNudgeHostedService>.Instance);
    }

    [Fact]
    public void Enabled_OnlyOutsideTesting_AndOnlyWithKeys()
    {
        Build("Testing", Configured).IsEnabled.Should().BeFalse();
        Build("Production", new PushOptions()).IsEnabled.Should().BeFalse();
        Build("Development", Configured).IsEnabled.Should().BeTrue();
        Build("Production", Configured).IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Disabled_ServiceFinishesAtOnce_WithoutTouchingTheContainer()
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Testing");
        var scopes = Substitute.For<IServiceScopeFactory>();
        var service = new PushNudgeHostedService(scopes, env, Options.Create(Configured), TimeProvider.System, NullLogger<PushNudgeHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);

        scopes.DidNotReceive().CreateScope();
    }
}
