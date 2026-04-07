using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;

namespace A2S.Tests.Shared.Builders;

public sealed class ExerciseBuilder
{
    private string _name = "Bench Press";
    private ExerciseCategory _category = ExerciseCategory.MainLift;
    private EquipmentType _equipment = EquipmentType.Barbell;
    private DayNumber _assignedDay = DayNumber.Day1;
    private int _orderInDay = 1;
    private string _externalTemplateId = "bench-press-barbell";
    private ProgressionType _progressionType = ProgressionType.Linear;

    // Linear defaults
    private decimal _trainingMaxValue = 100m;
    private bool _useAmrap = true;
    private int _baseSets = 4;

    // RPS defaults
    private int _repRangeMin = 8;
    private int _repRangeMax = 12;
    private int _startingSets = 2;
    private int _targetSets = 4;
    private bool _isUnilateral;
    private Weight? _startingWeight;

    // MinimalSets defaults
    private decimal _minSetsWeight = 32m;
    private int _targetTotalReps = 40;
    private int _minSetsStartingSets = 3;
    private int _minimumSets = 2;
    private int _maximumSets = 10;

    public ExerciseBuilder WithName(string name) { _name = name; return this; }
    public ExerciseBuilder WithCategory(ExerciseCategory category) { _category = category; return this; }
    public ExerciseBuilder WithEquipment(EquipmentType equipment) { _equipment = equipment; return this; }
    public ExerciseBuilder WithDay(DayNumber day) { _assignedDay = day; return this; }
    public ExerciseBuilder WithOrder(int order) { _orderInDay = order; return this; }
    public ExerciseBuilder WithTemplateId(string id) { _externalTemplateId = id; return this; }

    public ExerciseBuilder AsLinear(decimal trainingMax = 100m, bool useAmrap = true, int baseSets = 4)
    {
        _progressionType = ProgressionType.Linear;
        _trainingMaxValue = trainingMax;
        _useAmrap = useAmrap;
        _baseSets = baseSets;
        return this;
    }

    public ExerciseBuilder AsRepsPerSet(
        int repMin = 8, int repMax = 12,
        int startingSets = 2, int targetSets = 4,
        bool isUnilateral = false, Weight? startingWeight = null)
    {
        _progressionType = ProgressionType.RepsPerSet;
        _repRangeMin = repMin;
        _repRangeMax = repMax;
        _startingSets = startingSets;
        _targetSets = targetSets;
        _isUnilateral = isUnilateral;
        _startingWeight = startingWeight;
        return this;
    }

    public ExerciseBuilder AsMinimalSets(
        decimal weight = 32m, int targetTotalReps = 40,
        int startingSets = 3, int minimumSets = 2, int maximumSets = 10)
    {
        _progressionType = ProgressionType.MinimalSets;
        _minSetsWeight = weight;
        _targetTotalReps = targetTotalReps;
        _minSetsStartingSets = startingSets;
        _minimumSets = minimumSets;
        _maximumSets = maximumSets;
        return this;
    }

    public Exercise Build()
    {
        return _progressionType switch
        {
            ProgressionType.Linear => Exercise.CreateWithLinearProgression(
                _name, _category, _equipment, _assignedDay, _orderInDay,
                _externalTemplateId, TrainingMax.Create(_trainingMaxValue, WeightUnit.Kilograms),
                _useAmrap, _baseSets),

            ProgressionType.RepsPerSet => Exercise.CreateWithRepsPerSetProgression(
                _name, _category, _equipment, _assignedDay, _orderInDay,
                _externalTemplateId, RepRange.Create(_repRangeMin, _repRangeMax),
                _startingSets, _targetSets, _isUnilateral, _startingWeight),

            ProgressionType.MinimalSets => Exercise.CreateWithMinimalSetsProgression(
                _name, _category, _equipment, _assignedDay, _orderInDay,
                _externalTemplateId, Weight.Create(_minSetsWeight, WeightUnit.Kilograms),
                _targetTotalReps, _minSetsStartingSets, _minimumSets, _maximumSets),

            _ => throw new ArgumentOutOfRangeException(nameof(_progressionType))
        };
    }

    private enum ProgressionType { Linear, RepsPerSet, MinimalSets }
}
