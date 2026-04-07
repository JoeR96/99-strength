using A2S.Application.Behaviors;
using A2S.Application.Common;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using MediatR;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Behaviors;

public class AuthorizedWorkoutBehaviorTests
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly AuthorizedWorkoutBehavior<TestWorkoutCommand, Result> _behavior;

    private static readonly UserId TestUserId = new("ddd11111-1111-1111-1111-111111111111");
    private static readonly Guid TestWorkoutGuid = Guid.Parse("ddd22222-2222-2222-2222-222222222222");

    public AuthorizedWorkoutBehaviorTests()
    {
        _workoutRepository = Substitute.For<IWorkoutRepository>();
        _currentUserService = Substitute.For<ICurrentUserService>();
        _behavior = new AuthorizedWorkoutBehavior<TestWorkoutCommand, Result>(
            _workoutRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_WhenUserNotAuthenticated_ReturnsUnauthenticatedFailure()
    {
        _currentUserService.UserId.Returns((string?)null);
        var command = new TestWorkoutCommand(TestWorkoutGuid);
        RequestHandlerDelegate<Result> next = _ => Task.FromResult(Result.Success());

        var result = await _behavior.Handle(command, next, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.Unauthenticated);
    }

    [Fact]
    public async Task Handle_WhenWorkoutNotFound_ReturnsNotFoundFailure()
    {
        SetupAuthenticatedUser(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns((Workout?)null);
        var command = new TestWorkoutCommand(TestWorkoutGuid);
        RequestHandlerDelegate<Result> next = _ => Task.FromResult(Result.Success());

        var result = await _behavior.Handle(command, next, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.NotFound);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotOwnWorkout_ReturnsUnauthorizedFailure()
    {
        var otherUserId = new UserId(Guid.NewGuid().ToString());
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateWorkout(otherUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);
        var command = new TestWorkoutCommand(TestWorkoutGuid);
        RequestHandlerDelegate<Result> next = _ => Task.FromResult(Result.Success());

        var result = await _behavior.Handle(command, next, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.Unauthorized);
    }

    [Fact]
    public async Task Handle_WhenAuthorized_CallsNextDelegate()
    {
        SetupAuthenticatedUser(TestUserId);
        var workout = CreateWorkout(TestUserId);
        _workoutRepository.GetByIdAsync(Arg.Any<WorkoutId>(), Arg.Any<CancellationToken>())
            .Returns(workout);
        var command = new TestWorkoutCommand(TestWorkoutGuid);
        var nextCalled = false;
        RequestHandlerDelegate<Result> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success());
        };

        await _behavior.Handle(command, next, CancellationToken.None);

        nextCalled.Should().BeTrue();
    }

    private void SetupAuthenticatedUser(UserId userId)
    {
        _currentUserService.UserId.Returns(userId.Value.ToString());
    }

    private static Workout CreateWorkout(UserId userId)
    {
        var exercises = new List<Exercise>
        {
            Exercise.CreateWithLinearProgression(
                "Squat", ExerciseCategory.MainLift, EquipmentType.Barbell,
                DayNumber.Day1, 1, "TEST123",
                TrainingMax.Create(100m, WeightUnit.Kilograms), true, 3)
        };
        return Workout.Create(userId, "Test", ProgramVariant.FiveDay, exercises);
    }

    // Test command implementing IWorkoutCommand and IFailureFactory
    private record TestWorkoutCommand(Guid WorkoutId) : IWorkoutCommand<Result>, IFailureFactory<Result>
    {
        public static Result CreateFailure(string error, ErrorCode code) => Result.Failure(error, code);
    }
}
