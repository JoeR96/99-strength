using A2S.Application.Behaviors;
using A2S.Application.Commands.ProgressWeek;
using A2S.Application.Common;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Behaviors;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_WhenNoValidatorsExist_CallsNext()
    {
        var validators = Enumerable.Empty<IValidator<ProgressWeekCommand>>();
        var behavior = new ValidationBehavior<ProgressWeekCommand, Result<ProgressWeekResult>>(validators);
        var nextCalled = false;
        RequestHandlerDelegate<Result<ProgressWeekResult>> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success(new ProgressWeekResult
            {
                PreviousWeek = 1, NewWeek = 2, NewBlock = 1, IsDeloadWeek = false, IsProgramComplete = false
            }));
        };

        var command = new ProgressWeekCommand(Guid.Parse("aaa11111-1111-1111-1111-111111111111"));
        var result = await behavior.Handle(command, next, CancellationToken.None);

        nextCalled.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenValidatorPasses_CallsNext()
    {
        var validator = Substitute.For<IValidator<ProgressWeekCommand>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<ProgressWeekCommand>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        var behavior = new ValidationBehavior<ProgressWeekCommand, Result<ProgressWeekResult>>(new[] { validator });
        var nextCalled = false;
        RequestHandlerDelegate<Result<ProgressWeekResult>> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success(new ProgressWeekResult
            {
                PreviousWeek = 1, NewWeek = 2, NewBlock = 1, IsDeloadWeek = false, IsProgramComplete = false
            }));
        };

        var command = new ProgressWeekCommand(Guid.Parse("aaa11111-1111-1111-1111-111111111111"));
        var result = await behavior.Handle(command, next, CancellationToken.None);

        nextCalled.Should().BeTrue();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenValidatorFails_ThrowsValidationException()
    {
        var validator = Substitute.For<IValidator<ProgressWeekCommand>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<ProgressWeekCommand>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[]
            {
                new ValidationFailure("WorkoutId", "WorkoutId is required")
            }));
        var behavior = new ValidationBehavior<ProgressWeekCommand, Result<ProgressWeekResult>>(new[] { validator });
        RequestHandlerDelegate<Result<ProgressWeekResult>> next = _ =>
            Task.FromResult(Result.Success(new ProgressWeekResult
            {
                PreviousWeek = 1, NewWeek = 2, NewBlock = 1, IsDeloadWeek = false, IsProgramComplete = false
            }));

        var command = new ProgressWeekCommand(Guid.Empty);
        var act = () => behavior.Handle(command, next, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("WorkoutId is required");
    }

    [Fact]
    public async Task Handle_WhenMultipleValidatorsAndOneFails_ThrowsValidationException()
    {
        var passingValidator = Substitute.For<IValidator<ProgressWeekCommand>>();
        passingValidator.ValidateAsync(Arg.Any<ValidationContext<ProgressWeekCommand>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var failingValidator = Substitute.For<IValidator<ProgressWeekCommand>>();
        failingValidator.ValidateAsync(Arg.Any<ValidationContext<ProgressWeekCommand>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[]
            {
                new ValidationFailure("WorkoutId", "WorkoutId error")
            }));

        var behavior = new ValidationBehavior<ProgressWeekCommand, Result<ProgressWeekResult>>(
            new[] { passingValidator, failingValidator });
        RequestHandlerDelegate<Result<ProgressWeekResult>> next = _ =>
            Task.FromResult(Result.Success(new ProgressWeekResult
            {
                PreviousWeek = 1, NewWeek = 2, NewBlock = 1, IsDeloadWeek = false, IsProgramComplete = false
            }));

        var command = new ProgressWeekCommand(Guid.Empty);
        var act = () => behavior.Handle(command, next, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_WhenMultipleFailures_CollectsAllErrors()
    {
        var validator = Substitute.For<IValidator<ProgressWeekCommand>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<ProgressWeekCommand>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[]
            {
                new ValidationFailure("WorkoutId", "WorkoutId is required"),
                new ValidationFailure("WorkoutId", "WorkoutId must be valid")
            }));
        var behavior = new ValidationBehavior<ProgressWeekCommand, Result<ProgressWeekResult>>(new[] { validator });
        RequestHandlerDelegate<Result<ProgressWeekResult>> next = _ =>
            Task.FromResult(Result.Success(new ProgressWeekResult
            {
                PreviousWeek = 1, NewWeek = 2, NewBlock = 1, IsDeloadWeek = false, IsProgramComplete = false
            }));

        var command = new ProgressWeekCommand(Guid.Empty);
        var act = () => behavior.Handle(command, next, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WhenValidatorFails_DoesNotCallNext()
    {
        var validator = Substitute.For<IValidator<ProgressWeekCommand>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<ProgressWeekCommand>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new[]
            {
                new ValidationFailure("WorkoutId", "WorkoutId is required")
            }));
        var behavior = new ValidationBehavior<ProgressWeekCommand, Result<ProgressWeekResult>>(new[] { validator });
        var nextCalled = false;
        RequestHandlerDelegate<Result<ProgressWeekResult>> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult(Result.Success(new ProgressWeekResult
            {
                PreviousWeek = 1, NewWeek = 2, NewBlock = 1, IsDeloadWeek = false, IsProgramComplete = false
            }));
        };

        var command = new ProgressWeekCommand(Guid.Empty);
        var act = () => behavior.Handle(command, next, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        nextCalled.Should().BeFalse();
    }
}
