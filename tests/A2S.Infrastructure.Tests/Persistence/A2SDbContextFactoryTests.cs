using A2S.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace A2S.Infrastructure.Tests.Persistence;

public class A2SDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_WhenConnectionStringEnvVarNotSet_ThrowsInvalidOperationException()
    {
        var originalValue = Environment.GetEnvironmentVariable("A2S_CONNECTION_STRING");
        Environment.SetEnvironmentVariable("A2S_CONNECTION_STRING", null);

        try
        {
            var factory = new A2SDbContextFactory();

            var act = () => factory.CreateDbContext([]);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*A2S_CONNECTION_STRING*");
        }
        finally
        {
            Environment.SetEnvironmentVariable("A2S_CONNECTION_STRING", originalValue);
        }
    }
}
