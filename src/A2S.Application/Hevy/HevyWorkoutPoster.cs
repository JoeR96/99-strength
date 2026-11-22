using System.Text.Json;
using A2S.Application.Commands.GymSessions;
using A2S.Application.Interfaces;
using A2S.Application.Interfaces.Hevy;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace A2S.Application.Hevy;

/// <summary>
/// Writes a finished gym session to Hevy as a completed workout (<c>POST /v1/workouts</c>) from the server,
/// so the key never reaches the browser. Order in the workout: the ticked warm-up drills (warm-up sets), the
/// lifts as logged (normal sets, the AMRAP set as <c>failure</c>), then the finisher (Stair Machine (Floors)
/// for a stair finisher, otherwise the cardio kind's template). Real start and end times.
/// <para>
/// Never creates a second Hevy workout: once an attempt may have reached Hevy, the poster first looks for a
/// workout with the same title and start time and adopts it. Adopting (here or from the pull) stores the
/// Hevy id on the post, on the plan completion (so the pull dedupes) and as a <see cref="HevyLinkKind.Workout"/>
/// link keyed by the client session id.
/// </para>
/// </summary>
public sealed class HevyWorkoutPoster
{
    /// <summary>Hevy's built-in "Stair Machine (Floors)" (type floors_duration; scripts/hevy-exercises.json).</summary>
    public const string StairMachineFloorsTemplateId = "4377A52B";

    /// <summary>How many pages of recent Hevy workouts to search before posting again.</summary>
    public const int LookupPages = 3;

    private readonly IHevyClient _hevy;
    private readonly IHevyKeyResolver _keys;
    private readonly IHevyWorkoutPostRepository _posts;
    private readonly ITrainingPlanRepository _plans;
    private readonly IWorkoutRepository _workouts;
    private readonly IMobilityRoutineRepository _routines;
    private readonly ICardioProtocolRepository _protocols;
    private readonly IHevyLinkRepository _links;
    private readonly HevyTemplateCatalogue _catalogue;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalClock _clock;
    private readonly ILogger<HevyWorkoutPoster> _logger;
    private readonly ICardioTrackRepository? _tracks;

    public HevyWorkoutPoster(
        IHevyClient hevy,
        IHevyKeyResolver keys,
        IHevyWorkoutPostRepository posts,
        ITrainingPlanRepository plans,
        IWorkoutRepository workouts,
        IMobilityRoutineRepository routines,
        ICardioProtocolRepository protocols,
        IHevyLinkRepository links,
        HevyTemplateCatalogue catalogue,
        IUnitOfWork unitOfWork,
        ILocalClock clock,
        ILogger<HevyWorkoutPoster> logger,
        ICardioTrackRepository? tracks = null)
    {
        _tracks = tracks;
        _hevy = hevy ?? throw new ArgumentNullException(nameof(hevy));
        _keys = keys ?? throw new ArgumentNullException(nameof(keys));
        _posts = posts ?? throw new ArgumentNullException(nameof(posts));
        _plans = plans ?? throw new ArgumentNullException(nameof(plans));
        _workouts = workouts ?? throw new ArgumentNullException(nameof(workouts));
        _routines = routines ?? throw new ArgumentNullException(nameof(routines));
        _protocols = protocols ?? throw new ArgumentNullException(nameof(protocols));
        _links = links ?? throw new ArgumentNullException(nameof(links));
        _catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>One attempt. Returns the post's status afterwards; failures are recorded on the post, never thrown.</summary>
    public async Task<HevyPostStatus> PostAsync(HevyWorkoutPost post, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(post);
        if (post.Status is HevyPostStatus.Posted or HevyPostStatus.Skipped)
        {
            return post.Status;
        }

        var apiKey = await _keys.ForUserAsync(post.UserId, ct);
        if (string.IsNullOrEmpty(apiKey))
        {
            post.MarkSkipped("no Hevy key");
            return await SaveAsync(post, ct);
        }

        if (post.MayAlreadyExistInHevy)
        {
            var found = await FindExistingAsync(apiKey, post, ct);
            if (!found.Ok)
            {
                return await FailAsync(post, found.Error!, ct);
            }

            if (found.Value is { } existingId)
            {
                await AdoptAsync(post, existingId, ct);
                return await SaveAsync(post, ct);
            }
        }

        var draft = await BuildDraftAsync(apiKey, post, ct);
        if (!draft.Ok)
        {
            return await FailAsync(post, draft.Error!, ct);
        }

        var created = await _hevy.CreateWorkoutAsync(apiKey, draft.Value!, ct);
        if (!created.Ok)
        {
            return await FailAsync(post, created.Error!, ct);
        }

        if (string.IsNullOrWhiteSpace(created.Value!.Id))
        {
            // Hevy said yes without an id: count it as an attempt that may exist, so the next try looks first.
            post.RecordFailure("Hevy did not return the workout id", permanent: false, _clock.NowUtc());
            return await SaveAsync(post, ct);
        }

        await AdoptAsync(post, created.Value.Id, ct);
        _logger.LogInformation("Gym session posted to Hevy");
        return await SaveAsync(post, ct);
    }

    /// <summary>
    /// Marks the post as the given Hevy workout, stores the id on its plan completion and keeps a Workout link
    /// keyed by the client session id. Does not save; the caller's unit of work does.
    /// </summary>
    public async Task AdoptAsync(HevyWorkoutPost post, string hevyWorkoutId, CancellationToken ct = default)
    {
        var now = _clock.NowUtc();
        post.MarkPosted(hevyWorkoutId, now);
        _posts.Update(post);

        if (post.PlanId is { } planId && post.CompletionId is { } completionId)
        {
            var plan = await _plans.GetByIdAsync(planId, ct);
            if (plan is not null && plan.LinkCompletionToHevy(completionId, hevyWorkoutId))
            {
                _plans.Update(plan);
            }
        }

        var key = HevyLocalKeys.GymSession(post.ClientSessionId);
        var link = await _links.FindAsync(post.UserId, HevyLinkKind.Workout, key, ct);
        if (link is null)
        {
            await _links.AddAsync(HevyLink.Create(post.UserId, HevyLinkKind.Workout, key, hevyWorkoutId, now), ct);
        }
        else
        {
            link.Relink(hevyWorkoutId, now);
            _links.Update(link);
        }
    }

    private async Task<HevyResult<string?>> FindExistingAsync(string apiKey, HevyWorkoutPost post, CancellationToken ct)
    {
        for (var page = 1; page <= LookupPages; page++)
        {
            var result = await _hevy.GetWorkoutsAsync(apiKey, page, ct);
            if (!result.Ok)
            {
                return HevyResult<string?>.Failure(result.Error!);
            }

            var match = result.Value!.Items.FirstOrDefault(w => post.Describes(w.Title, w.StartTime));
            if (match is not null)
            {
                return HevyResult<string?>.Success(match.Id);
            }

            // Newest first: once a page is older than the session, it is not there.
            if (!result.Value.HasMore || result.Value.Items.All(w => w.StartTime < post.StartedAtUtc.AddMinutes(-1)))
            {
                break;
            }
        }

        return HevyResult<string?>.Success(null);
    }

    private async Task<HevyResult<HevyWorkoutDraft>> BuildDraftAsync(string apiKey, HevyWorkoutPost post, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<GymSessionPayload>(post.PayloadJson, FinishGymSessionCommandHandler.Json)
            ?? new GymSessionPayload();
        var exercises = new List<HevyWorkoutExerciseDraft>();

        // A walk, run or cardio session, or a home routine, logged outside gym mode: one exercise.
        if (payload.Cardio is { } cardio)
        {
            var built = await CardioExerciseAsync(apiKey, post.UserId, cardio, ct);
            if (!built.Ok)
            {
                return HevyResult<HevyWorkoutDraft>.Failure(built.Error!);
            }

            exercises.Add(built.Value!);
            return HevyResult<HevyWorkoutDraft>.Success(Draft(post, exercises));
        }

        if (payload.Routine is { } routineSession)
        {
            exercises.Add(await RoutineExerciseAsync(routineSession, ct));
            return HevyResult<HevyWorkoutDraft>.Success(Draft(post, exercises));
        }

        // 1. Warm-up drills that were ticked, as warm-up sets.
        if (payload.WarmUpRoutineId is { } routineId)
        {
            var routine = await _routines.GetByIdAsync(new MobilityRoutineId(routineId), ct);
            if (routine is not null)
            {
                for (var i = 0; i < routine.Drills.Count; i++)
                {
                    var ticked = payload.WarmUpTicks is null || (i < payload.WarmUpTicks.Count && payload.WarmUpTicks[i]);
                    if (!ticked)
                    {
                        continue;
                    }

                    var drill = routine.Drills[i];
                    var template = await _catalogue.ForDrillAsync(apiKey, post.UserId, drill, ct);
                    if (!template.Ok)
                    {
                        return HevyResult<HevyWorkoutDraft>.Failure(template.Error!);
                    }

                    exercises.Add(new HevyWorkoutExerciseDraft { ExerciseTemplateId = template.Value!, Sets = DrillSets(drill) });
                }
            }
        }

        // 2. The lifts as logged.
        var program = await _workouts.GetByIdAsync(new WorkoutId(payload.WorkoutId), ct);
        foreach (var performance in payload.Performances.Where(p => p.CompletedSets.Count > 0))
        {
            var exercise = program?.Exercises.FirstOrDefault(e => e.Id.Value == performance.ExerciseId);
            if (exercise is null)
            {
                continue;
            }

            var template = await _catalogue.ForExerciseAsync(apiKey, post.UserId, exercise, ct);
            if (!template.Ok)
            {
                return HevyResult<HevyWorkoutDraft>.Failure(template.Error!);
            }

            var logged = performance.CompletedSets.OrderBy(s => s.SetNumber).ToList();
            if (performance.ExtraSet is { } extra)
            {
                // The bonus set goes last; it was never part of progression.
                logged.Add(extra with { WasAmrap = false });
            }

            var sideNotes = RoutineBuilder.SideNotes(exercise).ToList();
            exercises.Add(new HevyWorkoutExerciseDraft
            {
                ExerciseTemplateId = template.Value!,
                Notes = sideNotes.Count == 0 ? null : string.Join(" · ", sideNotes),
                Sets = logged.Select(s => new HevyWorkoutSetDraft
                {
                    Type = s.WasAmrap ? HevySetTypes.Failure : HevySetTypes.Normal,
                    WeightKg = s.WeightUnit == WeightUnit.Kilograms ? s.Weight : Math.Round(Weight.Create(s.Weight, s.WeightUnit).ConvertTo(WeightUnit.Kilograms).Value, 2),
                    Reps = s.ActualReps
                }).ToList()
            });
        }

        // 3. The finisher. A fixed cool-down goes on Treadmill (or Walking) with its duration.
        if (payload.Finisher is { } cooldown && payload.FinisherFixedKind is { } fixedKind)
        {
            var treadmill = string.Equals(fixedKind, "Treadmill", StringComparison.OrdinalIgnoreCase);
            exercises.Add(new HevyWorkoutExerciseDraft
            {
                ExerciseTemplateId = treadmill ? HevyTemplateCatalogue.Treadmill : HevyTemplateCatalogue.Walking,
                Notes = payload.FinisherFixedNotes is { } fixedNotes ? $"Cool-down · {fixedNotes}" : "Cool-down",
                Sets = [new HevyWorkoutSetDraft { DurationSeconds = cooldown.DurationSeconds, Rpe = cooldown.Effort.ToRpe() }]
            });
        }
        else if (payload.Finisher is { } finisher)
        {
            var protocol = payload.FinisherProtocolId is { } protocolId ? await _protocols.GetByIdAsync(new CardioProtocolId(protocolId), ct) : null;
            var track = payload.FinisherTrackId is { } trackId && _tracks is not null ? await _tracks.GetByIdAsync(new CardioTrackId(trackId), ct) : null;
            var stairs = track is not null
                ? track.Modality == CardioModality.StairClimber
                : finisher.Floors is not null || protocol is null || protocol.Name.Contains("stair", StringComparison.OrdinalIgnoreCase);
            string templateId;
            if (track is not null && !stairs)
            {
                templateId = HevyTemplateCatalogue.BuiltInTemplateFor(track);
            }
            else if (stairs)
            {
                templateId = StairMachineFloorsTemplateId;
            }
            else
            {
                var template = await _catalogue.ForCardioAsync(apiKey, post.UserId, protocol!.Kind, ct);
                if (!template.Ok)
                {
                    return HevyResult<HevyWorkoutDraft>.Failure(template.Error!);
                }

                templateId = template.Value!;
            }

            exercises.Add(new HevyWorkoutExerciseDraft
            {
                ExerciseTemplateId = templateId,
                Notes = (track is null ? string.Empty : $"{track.Name} · ")
                    + (finisher.Level is { } level ? $"Level {level} · {finisher.Effort}" : finisher.Effort.ToString()),
                Sets =
                [
                    new HevyWorkoutSetDraft
                    {
                        DurationSeconds = finisher.DurationSeconds,
                        CustomMetric = stairs ? finisher.Floors ?? 0 : null,
                        Rpe = finisher.Effort.ToRpe()
                    }
                ]
            });
        }

        return HevyResult<HevyWorkoutDraft>.Success(Draft(post, exercises));
    }

    private static HevyWorkoutDraft Draft(HevyWorkoutPost post, List<HevyWorkoutExerciseDraft> exercises) => new()
    {
        Title = post.Title,
        Description = "Logged in 99 Strength",
        StartTime = post.StartedAtUtc,
        EndTime = post.EndedAtUtc,
        Exercises = exercises
    };

    /// <summary>Walks and runs on Hevy's Walking / Treadmill / Running, stairs on Stair Machine (Floors), protocols on their kind's template.</summary>
    private async Task<HevyResult<HevyWorkoutExerciseDraft>> CardioExerciseAsync(string apiKey, UserId userId, CardioSessionPost cardio, CancellationToken ct)
    {
        var track = cardio.CardioTrackId is { } trackId && _tracks is not null ? await _tracks.GetByIdAsync(new CardioTrackId(trackId), ct) : null;
        string templateId;
        if (track is not null)
        {
            templateId = HevyTemplateCatalogue.BuiltInTemplateFor(track);
        }
        else
        {
            var protocol = cardio.CardioProtocolId is { } protocolId ? await _protocols.GetByIdAsync(new CardioProtocolId(protocolId), ct) : null;
            if (protocol is null)
            {
                templateId = HevyTemplateCatalogue.Walking;
            }
            else
            {
                var template = await _catalogue.ForCardioAsync(apiKey, userId, protocol.Kind, ct);
                if (!template.Ok)
                {
                    return HevyResult<HevyWorkoutExerciseDraft>.Failure(template.Error!);
                }

                templateId = template.Value!;
            }
        }

        var stairs = templateId == HevyTemplateCatalogue.StairMachineFloors;
        var notes = new List<string>();
        if (!string.IsNullOrWhiteSpace(cardio.Prescription))
        {
            notes.Add(cardio.Prescription!);
        }

        if (cardio.IntensityValue is { } intensity && track is not null)
        {
            notes.Add(DoubleProgressionStrategy.FormatIntensity(track.CurrentStage.DoubleProgression?.IntensityUnit ?? IntensityUnit.Level, intensity));
        }

        return HevyResult<HevyWorkoutExerciseDraft>.Success(new HevyWorkoutExerciseDraft
        {
            ExerciseTemplateId = templateId,
            Notes = notes.Count == 0 ? null : string.Join(" · ", notes),
            Sets =
            [
                new HevyWorkoutSetDraft
                {
                    DurationSeconds = cardio.DurationSeconds,
                    DistanceMeters = stairs ? null : cardio.DistanceMeters,
                    CustomMetric = stairs ? cardio.Floors ?? 0 : null,
                    Rpe = cardio.Rpe
                }
            ]
        });
    }

    /// <summary>A home routine as one Stretching (or Yoga, for a yoga flow or follow-along) block, the drills done in the notes.</summary>
    private async Task<HevyWorkoutExerciseDraft> RoutineExerciseAsync(RoutineSessionPost routineSession, CancellationToken ct)
    {
        var routine = await _routines.GetByIdAsync(new MobilityRoutineId(routineSession.MobilityRoutineId), ct);
        var drills = routine?.Drills
            .Where((_, i) => routineSession.DrillTicks is null || (i < routineSession.DrillTicks.Count && routineSession.DrillTicks[i]))
            .Select(d => d.Name)
            .ToList() ?? [];
        var yoga = routine?.PostsToHevyAsYoga ?? routineSession.Name.Contains("yoga", StringComparison.OrdinalIgnoreCase);
        return new HevyWorkoutExerciseDraft
        {
            ExerciseTemplateId = yoga ? HevyTemplateCatalogue.Yoga : HevyTemplateCatalogue.Stretching,
            Notes = drills.Count == 0 ? routineSession.Name : $"{routineSession.Name}: {string.Join(", ", drills)}",
            Sets = [new HevyWorkoutSetDraft { DurationSeconds = Math.Max(60, routineSession.DurationSeconds) }]
        };
    }

    private static List<HevyWorkoutSetDraft> DrillSets(MobilityDrill drill)
    {
        var count = Math.Max(drill.Sets, 1) * (drill.PerSide ? 2 : 1);
        return Enumerable.Range(0, count).Select(_ => drill.Dose.Unit == DoseUnit.Seconds
            ? new HevyWorkoutSetDraft { Type = HevySetTypes.Warmup, DurationSeconds = drill.Dose.Amount }
            : new HevyWorkoutSetDraft { Type = HevySetTypes.Warmup, Reps = drill.Dose.Amount }).ToList();
    }

    private async Task<HevyPostStatus> FailAsync(HevyWorkoutPost post, HevyError error, CancellationToken ct)
    {
        var permanent = error.Kind is HevyErrorKind.InvalidKey or HevyErrorKind.ProNeeded or HevyErrorKind.BadRequest;
        post.RecordFailure(error.Message, permanent, _clock.NowUtc());
        _logger.LogWarning("Gym session Hevy post failed ({Kind}); attempt {Attempt}, status {Status}", error.Kind, post.Attempts, post.Status);
        return await SaveAsync(post, ct);
    }

    private async Task<HevyPostStatus> SaveAsync(HevyWorkoutPost post, CancellationToken ct)
    {
        _posts.Update(post);
        await _unitOfWork.SaveChangesAsync(ct);
        return post.Status;
    }
}
