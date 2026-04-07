using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;

namespace A2S.Tests.Shared.Builders;

public sealed class WorkoutBuilder
{
    private UserId _userId = new("user_test123");
    private string _name = "Test Workout";
    private ProgramVariant _variant = ProgramVariant.FiveDay;
    private List<int>? _blockSequence;
    private readonly List<Exercise> _exercises = [];

    public WorkoutBuilder WithUserId(string userId) { _userId = new UserId(userId); return this; }
    public WorkoutBuilder WithUserId(UserId userId) { _userId = userId; return this; }
    public WorkoutBuilder WithName(string name) { _name = name; return this; }
    public WorkoutBuilder WithVariant(ProgramVariant variant) { _variant = variant; return this; }
    public WorkoutBuilder WithBlockSequence(List<int> sequence) { _blockSequence = sequence; return this; }

    public WorkoutBuilder WithExercise(Exercise exercise)
    {
        _exercises.Add(exercise);
        return this;
    }

    public WorkoutBuilder WithExercise(Action<ExerciseBuilder> configure)
    {
        var builder = new ExerciseBuilder();
        configure(builder);
        _exercises.Add(builder.Build());
        return this;
    }

    public WorkoutBuilder WithDefaultLinearExercise(
        string name = "Bench Press",
        DayNumber day = DayNumber.Day1,
        int order = 1,
        decimal trainingMax = 100m)
    {
        _exercises.Add(new ExerciseBuilder()
            .WithName(name)
            .WithDay(day)
            .WithOrder(order)
            .AsLinear(trainingMax)
            .Build());
        return this;
    }

    public Workout Build()
    {
        if (_exercises.Count == 0)
        {
            WithDefaultLinearExercise();
        }

        return Workout.Create(_userId, _name, _variant, _exercises, _blockSequence);
    }
}
