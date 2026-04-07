using A2S.Domain.Aggregates.Workout;
using A2S.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace A2S.Infrastructure.Tests.Persistence;

public class WorkoutConcurrencyTokenTests
{
    [Fact]
    public void WorkoutConfiguration_ConfiguresXminConcurrencyToken()
    {
        var options = new DbContextOptionsBuilder<A2SDbContext>()
            .UseNpgsql("Host=localhost")
            .Options;

        using var context = new A2SDbContext(options);
        var model = context.Model;
        var workoutEntity = model.FindEntityType(typeof(Workout))!;

        var xminProperty = workoutEntity.FindProperty("xmin");
        xminProperty.Should().NotBeNull("xmin property should be configured");
        xminProperty!.IsConcurrencyToken.Should().BeTrue("xmin should be a concurrency token");
        xminProperty.GetColumnType().Should().Be("xid");
    }
}
