using FluentValidation;

namespace A2S.Application.Queries.SimulateWorkout;

public sealed class SimulateWorkoutQueryValidator : AbstractValidator<SimulateWorkoutQuery>
{
    public SimulateWorkoutQueryValidator()
    {
        RuleFor(x => x.WorkoutId).NotEmpty();
        RuleFor(x => x.SessionCount).InclusiveBetween(1, 500);
    }
}
