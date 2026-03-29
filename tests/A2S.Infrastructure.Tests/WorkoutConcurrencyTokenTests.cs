using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Infrastructure.Persistence.Configurations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Xunit;

namespace A2S.Infrastructure.Tests;

public class WorkoutConcurrencyTokenTests
{
    [Fact]
    public void WorkoutConfiguration_ConfiguresXminConcurrencyToken()
    {
        // Arrange
        var conventionSet = new ConventionSet();
        var modelBuilder = new ModelBuilder(conventionSet);

        var configuration = new WorkoutConfiguration();

        // Act
        configuration.Configure(modelBuilder.Entity<Workout>());

        var model = modelBuilder.FinalizeModel();
        var workoutEntity = model.FindEntityType(typeof(Workout))!;

        // Assert — check that xmin shadow property exists and is a concurrency token
        var xminProperty = workoutEntity.FindProperty("xmin");
        xminProperty.Should().NotBeNull("xmin property should be configured");
        xminProperty!.IsConcurrencyToken.Should().BeTrue("xmin should be a concurrency token");
        xminProperty.GetColumnType().Should().Be("xid");
    }
}
