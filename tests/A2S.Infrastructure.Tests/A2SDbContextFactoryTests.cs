using A2S.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;

namespace A2S.Infrastructure.Tests;

public class A2SDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_WhenConnectionStringEnvVarNotSet_ThrowsInvalidOperationException()
    {
        // Arrange
        var originalValue = Environment.GetEnvironmentVariable("A2S_CONNECTION_STRING");
        Environment.SetEnvironmentVariable("A2S_CONNECTION_STRING", null);

        try
        {
            var factory = new A2SDbContextFactory();

            // Act
            var act = () => factory.CreateDbContext([]);

            // Assert
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*A2S_CONNECTION_STRING*");
        }
        finally
        {
            Environment.SetEnvironmentVariable("A2S_CONNECTION_STRING", originalValue);
        }
    }
}
