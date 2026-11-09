using A2S.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace A2S.Infrastructure.Tests.Services;

public class HevyPollHostedServiceTests
{
    private static HevyPollHostedService Build(string environment, IServiceScopeFactory? scopes = null)
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environment);
        return new HevyPollHostedService(scopes ?? Substitute.For<IServiceScopeFactory>(), env, TimeProvider.System, NullLogger<HevyPollHostedService>.Instance);
    }

    [Fact]
    public void Enabled_EverywhereButTesting()
    {
        Build("Testing").IsEnabled.Should().BeFalse();
        Build("Development").IsEnabled.Should().BeTrue();
        Build("Production").IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Interval_IsFifteenMinutes()
    {
        HevyPollHostedService.Interval.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task InTesting_ServiceFinishesAtOnce_WithoutTouchingTheContainer()
    {
        var scopes = Substitute.For<IServiceScopeFactory>();
        var service = Build("Testing", scopes);

        await service.StartAsync(CancellationToken.None);
        await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);

        scopes.DidNotReceive().CreateScope();
    }
}
