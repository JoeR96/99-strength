using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using FluentAssertions;
using Xunit;

namespace A2S.Infrastructure.Tests.Repositories;

public class WorkoutRepositoryUserScopingTests
{
    [Fact]
    public void IWorkoutRepository_GetAllAsync_RequiresUserId()
    {
        // Verify the interface signature requires UserId parameter
        var method = typeof(IWorkoutRepository).GetMethod("GetAllAsync");
        method.Should().NotBeNull();
        var parameters = method!.GetParameters();
        parameters.Should().Contain(p => p.ParameterType == typeof(UserId),
            "GetAllAsync must require UserId to prevent cross-tenant data leaks");
    }

    [Fact]
    public void IWorkoutRepository_GetByStatusAsync_RequiresUserId()
    {
        // Verify the interface signature requires UserId parameter
        var method = typeof(IWorkoutRepository).GetMethod("GetByStatusAsync");
        method.Should().NotBeNull();
        var parameters = method!.GetParameters();
        parameters.Should().Contain(p => p.ParameterType == typeof(UserId),
            "GetByStatusAsync must require UserId to prevent cross-tenant data leaks");
        parameters.Should().Contain(p => p.ParameterType == typeof(WorkoutStatus));
    }

    [Fact]
    public void IWorkoutRepository_GetActiveWorkoutAsync_RequiresUserId()
    {
        var method = typeof(IWorkoutRepository).GetMethod("GetActiveWorkoutAsync");
        method.Should().NotBeNull();
        var parameters = method!.GetParameters();
        parameters.Should().Contain(p => p.ParameterType == typeof(UserId),
            "GetActiveWorkoutAsync must require UserId to prevent cross-tenant data leaks");
    }

    [Fact]
    public void IWorkoutRepository_GetAllByUserSummaryAsync_RequiresUserId()
    {
        var method = typeof(IWorkoutRepository).GetMethod("GetAllByUserSummaryAsync");
        method.Should().NotBeNull();
        var parameters = method!.GetParameters();
        parameters.Should().Contain(p => p.ParameterType == typeof(UserId),
            "GetAllByUserSummaryAsync must require UserId to prevent cross-tenant data leaks");
    }
}
