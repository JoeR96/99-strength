using A2S.Application.Interfaces.Hevy;
using A2S.Application.Services;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.Services;

namespace A2S.Application.Hevy;

/// <summary>
/// Resolves a local exercise, mobility drill or cardio protocol to the Hevy exercise template id a
/// routine needs. Library exercises carry their Hevy id already; anything Hevy has no template for
/// (a <c>custom:</c> exercise, every drill, each cardio kind) is created once as a custom template and
/// remembered as a <see cref="HevyLinkKind.Template"/> link, so a second push reuses it.
/// </summary>
public sealed class HevyTemplateCatalogue
{
    public const string CustomPrefix = "custom:";

    private const string DrillMuscleGroup = "full_body";
    private const string CardioMuscleGroup = "cardio";
    private const string FallbackMuscleGroup = "other";

    /// <summary>Hevy's <c>primary_muscle_group</c> vocabulary (api.hevyapp.com/docs).</summary>
    private static readonly HashSet<string> HevyMuscleGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        "abdominals", "abductors", "adductors", "biceps", "calves", "cardio", "chest", "forearms", "full_body",
        "glutes", "hamstrings", "lats", "lower_back", "neck", "quadriceps", "shoulders", "traps", "triceps",
        "upper_back", "other"
    };

    private readonly IHevyClient _hevy;
    private readonly IHevyLinkRepository _links;
    private readonly IExerciseLibraryProvider _library;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalClock _clock;
    private readonly Dictionary<(UserId, string), string> _cache = new();

    public HevyTemplateCatalogue(
        IHevyClient hevy,
        IHevyLinkRepository links,
        IExerciseLibraryProvider library,
        IUnitOfWork unitOfWork,
        ILocalClock clock)
    {
        _hevy = hevy ?? throw new ArgumentNullException(nameof(hevy));
        _links = links ?? throw new ArgumentNullException(nameof(links));
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    // Hevy's built-in cardio templates (ids checked against scripts/hevy-exercises.json).
    public const string StairMachineFloors = "4377A52B"; // floors_duration
    public const string StairMachineSteps = "4377A52C";  // steps_duration
    public const string Walking = "33EDD7DB";            // distance_duration
    public const string Treadmill = "243710DE";          // distance_duration
    public const string Running = "AC1BB830";            // distance_duration
    public const string Stretching = "527DA061";         // duration
    public const string Yoga = "8C9D2928";               // duration

    /// <summary>The built-in template a cardio track is pushed as: stairs → Stair Machine (Floors), treadmill walks → Treadmill.</summary>
    public static string BuiltInTemplateFor(CardioTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        return track.Modality switch
        {
            CardioModality.StairClimber => StairMachineFloors,
            CardioModality.Run => Running,
            CardioModality.Walk when track.CurrentStage.DoubleProgression?.IntensityUnit is IntensityUnit.InclinePercent or IntensityUnit.SpeedKmh
                || (track.Notes?.Contains("treadmill", StringComparison.OrdinalIgnoreCase) ?? false) => Treadmill,
            _ => Walking
        };
    }

    /// <summary>Which track modalities a built-in Hevy cardio template can log against (empty for anything else).</summary>
    public static IReadOnlyList<CardioModality> ModalitiesForTemplate(string? templateId) => templateId switch
    {
        StairMachineFloors or StairMachineSteps => [CardioModality.StairClimber],
        Walking => [CardioModality.Walk],
        Running => [CardioModality.Run],
        Treadmill => [CardioModality.Walk, CardioModality.Run],
        _ => []
    };

    public static bool IsBuiltInCardio(string? templateId) => ModalitiesForTemplate(templateId).Count > 0;

    public static bool IsCustom(string? externalTemplateId) =>
        externalTemplateId is not null && externalTemplateId.StartsWith(CustomPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>The link key for a <c>custom:</c> exercise id (<c>exercise:&lt;slug&gt;</c>).</summary>
    public static string ExerciseTemplateKey(string externalTemplateId) =>
        $"exercise:{HevyLocalKeys.Slug(externalTemplateId[CustomPrefix.Length..])}";

    /// <summary>A library exercise's own Hevy id, or the custom template created for a <c>custom:</c> one.</summary>
    public Task<HevyResult<string>> ForExerciseAsync(string apiKey, UserId userId, Exercise exercise, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        if (!IsCustom(exercise.ExternalTemplateId))
        {
            return Task.FromResult(HevyResult<string>.Success(exercise.ExternalTemplateId));
        }

        var muscleGroup = _library.GetByName(exercise.Name)?.MuscleGroup;
        var draft = new HevyExerciseTemplateDraft
        {
            Title = exercise.Name,
            ExerciseType = exercise.Equipment == EquipmentType.Bodyweight ? HevyExerciseTypes.BodyweightReps : HevyExerciseTypes.WeightReps,
            EquipmentCategory = exercise.Equipment switch
            {
                EquipmentType.Barbell => "barbell",
                EquipmentType.Dumbbell => "dumbbell",
                EquipmentType.Cable or EquipmentType.Machine or EquipmentType.SmithMachine or EquipmentType.PlateLoadedMachine => "machine",
                _ => "none"
            },
            PrimaryMuscleGroup = HevyMuscleGroups.Contains(muscleGroup ?? string.Empty) ? muscleGroup!.ToLowerInvariant() : FallbackMuscleGroup
        };

        return EnsureAsync(apiKey, userId, ExerciseTemplateKey(exercise.ExternalTemplateId), draft, ct);
    }

    /// <summary>A drill's template: <c>duration</c> for timed doses, <c>reps_only</c> for reps and breaths.</summary>
    public Task<HevyResult<string>> ForDrillAsync(string apiKey, UserId userId, MobilityDrill drill, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(drill);
        var draft = new HevyExerciseTemplateDraft
        {
            Title = drill.Name,
            ExerciseType = drill.Dose.Unit == DoseUnit.Seconds ? HevyExerciseTypes.Duration : HevyExerciseTypes.RepsOnly,
            PrimaryMuscleGroup = DrillMuscleGroup
        };

        return EnsureAsync(apiKey, userId, HevyLocalKeys.DrillTemplate(drill.Name), draft, ct);
    }

    /// <summary>One template per cardio kind: intervals as <c>duration</c>, LISS as <c>distance_duration</c>.</summary>
    public Task<HevyResult<string>> ForCardioAsync(string apiKey, UserId userId, CardioProtocolKind kind, CancellationToken ct = default)
    {
        var draft = new HevyExerciseTemplateDraft
        {
            Title = CardioTemplateTitle(kind),
            ExerciseType = kind == CardioProtocolKind.Liss ? HevyExerciseTypes.DistanceDuration : HevyExerciseTypes.Duration,
            PrimaryMuscleGroup = CardioMuscleGroup
        };

        return EnsureAsync(apiKey, userId, HevyLocalKeys.CardioTemplate(kind), draft, ct);
    }

    public static string CardioTemplateTitle(CardioProtocolKind kind) => kind switch
    {
        CardioProtocolKind.NorwegianFourByFour => "Norwegian 4×4",
        CardioProtocolKind.CouchToFiveK => "Couch to 5K",
        CardioProtocolKind.Liss => "LISS",
        _ => kind.ToString()
    };

    /// <summary>Every template link the user has, Hevy id → local key (drills, cardio kinds, custom exercises).</summary>
    public async Task<IReadOnlyDictionary<string, string>> TemplateIdsAsync(UserId userId, CancellationToken ct = default)
    {
        var links = await _links.ListAsync(userId, HevyLinkKind.Template, ct);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var link in links)
        {
            map[link.HevyId] = link.LocalKey;
        }

        return map;
    }

    private async Task<HevyResult<string>> EnsureAsync(string apiKey, UserId userId, string localKey, HevyExerciseTemplateDraft draft, CancellationToken ct)
    {
        if (_cache.TryGetValue((userId, localKey), out var cached))
        {
            return HevyResult<string>.Success(cached);
        }

        var existing = await _links.FindAsync(userId, HevyLinkKind.Template, localKey, ct);
        if (existing is not null)
        {
            _cache[(userId, localKey)] = existing.HevyId;
            return HevyResult<string>.Success(existing.HevyId);
        }

        var created = await _hevy.CreateExerciseTemplateAsync(apiKey, draft, ct);
        if (!created.Ok)
        {
            return HevyResult<string>.Failure(created.Error!);
        }

        var templateId = created.Value!.Id;
        await _links.AddAsync(HevyLink.Create(userId, HevyLinkKind.Template, localKey, templateId, _clock.NowUtc()), ct);
        // Saved straight away: a routine push that fails afterwards must not lose the template it created.
        await _unitOfWork.SaveChangesAsync(ct);
        _cache[(userId, localKey)] = templateId;
        return HevyResult<string>.Success(templateId);
    }
}
