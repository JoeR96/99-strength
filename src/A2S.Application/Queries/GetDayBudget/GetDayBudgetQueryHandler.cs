using A2S.Application.Common;
using A2S.Application.DTOs;
using A2S.Application.Services;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Queries.GetDayBudget;

public sealed class GetDayBudgetQueryHandler : IRequestHandler<GetDayBudgetQuery, Result<DayBudgetDto>>
{
    private readonly IWorkoutRepository _workoutRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly DayBudgetCalculator _calculator;

    public GetDayBudgetQueryHandler(
        IWorkoutRepository workoutRepository,
        ICurrentUserService currentUserService,
        DayBudgetCalculator calculator)
    {
        _workoutRepository = workoutRepository ?? throw new ArgumentNullException(nameof(workoutRepository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
    }

    public async Task<Result<DayBudgetDto>> Handle(GetDayBudgetQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();
        if (userId == null)
        {
            return Result.Failure<DayBudgetDto>("User must be authenticated.", ErrorCode.Unauthenticated);
        }

        var workout = await _workoutRepository.GetByIdAsync(new WorkoutId(request.WorkoutId), cancellationToken);
        if (workout == null)
        {
            return Result.Failure<DayBudgetDto>("Workout not found.", ErrorCode.NotFound);
        }

        if (workout.UserId != userId.Value)
        {
            return Result.Failure<DayBudgetDto>("You can only view your own workouts.", ErrorCode.Unauthorized);
        }

        var weekNumber = request.WeekNumber ?? DayBudgetCalculator.CurrentWeekOf(workout);
        if (weekNumber < 1 || weekNumber > workout.TotalWeeks)
        {
            return Result.Failure<DayBudgetDto>(
                $"Week number must be between 1 and {workout.TotalWeeks}.", ErrorCode.ValidationFailed);
        }

        var daysPerWeek = workout.GetDaysPerWeek();
        if ((int)request.Day < 1 || (int)request.Day > daysPerWeek)
        {
            return Result.Failure<DayBudgetDto>(
                $"Day number must be between 1 and {daysPerWeek}.", ErrorCode.ValidationFailed);
        }

        var budget = await _calculator.ForDayAsync(workout, request.Day, weekNumber, cancellationToken);
        return Result.Success(budget);
    }
}
