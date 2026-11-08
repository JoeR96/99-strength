using A2S.Application.Common;
using A2S.Application.Interfaces.Hevy;
using A2S.Application.SuggestedPlans;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using Microsoft.Extensions.Logging;

namespace A2S.Application.Hevy;

/// <summary>What one push did: the Hevy routine it left behind and whether it was created or updated.</summary>
public sealed record PushRoutineResult(string RoutineId, string Title, string Outcome)
{
    public const string Created = "created";
    public const string Updated = "updated";
}

/// <summary>
/// Pushes one scheduled session to Hevy as a routine, idempotently: an existing
/// <see cref="HevyLinkKind.Routine"/> link means <c>PUT</c>; none means <c>POST</c> and a new link.
/// A <c>POST</c> that times out may still have created the routine, so the pusher looks the title up
/// before giving up, and links it when found (review focus 3). The plan's folder and every template
/// are resolved and linked the same way.
/// </summary>
public sealed class HevyRoutinePusher
{
    /// <summary>How many pages of routines or folders to scan when looking something up by title.</summary>
    public const int MaxLookupPages = 10;

    private readonly IHevyClient _hevy;
    private readonly IHevyLinkRepository _links;
    private readonly HevyTemplateCatalogue _catalogue;
    private readonly IWorkoutRepository _workouts;
    private readonly ICardioProtocolRepository _protocols;
    private readonly IMobilityRoutineRepository _routines;
    private readonly IA2SProgramProvider _program;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalClock _clock;
    private readonly ILogger<HevyRoutinePusher> _logger;

    public HevyRoutinePusher(
        IHevyClient hevy,
        IHevyLinkRepository links,
        HevyTemplateCatalogue catalogue,
        IWorkoutRepository workouts,
        ICardioProtocolRepository protocols,
        IMobilityRoutineRepository routines,
        IA2SProgramProvider program,
        IUnitOfWork unitOfWork,
        ILocalClock clock,
        ILogger<HevyRoutinePusher> logger)
    {
        _hevy = hevy ?? throw new ArgumentNullException(nameof(hevy));
        _links = links ?? throw new ArgumentNullException(nameof(links));
        _catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
        _workouts = workouts ?? throw new ArgumentNullException(nameof(workouts));
        _protocols = protocols ?? throw new ArgumentNullException(nameof(protocols));
        _routines = routines ?? throw new ArgumentNullException(nameof(routines));
        _program = program ?? throw new ArgumentNullException(nameof(program));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<PushRoutineResult>> PushAsync(string apiKey, TrainingPlan plan, ScheduledSession session, DateOnly date, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(session);

        if (session.Kind == SessionKind.Rest)
        {
            return Result.Failure<PushRoutineResult>("A rest day has nothing to push to Hevy.", ErrorCode.ValidationFailed);
        }

        var folder = await EnsureFolderAsync(apiKey, plan, ct);
        if (folder.IsFailure)
        {
            return PlanAccess.Fail<PushRoutineResult>(folder);
        }

        var draft = session.Kind switch
        {
            SessionKind.Lift => await BuildLiftAsync(apiKey, plan, session, folder.Value, ct),
            SessionKind.Cardio => await BuildCardioAsync(apiKey, plan, session, folder.Value, ct),
            _ => await BuildMobilityAsync(apiKey, plan, session, folder.Value, ct)
        };
        if (draft.IsFailure)
        {
            return PlanAccess.Fail<PushRoutineResult>(draft);
        }

        var localKey = HevyLocalKeys.Routine(plan.Id, date, session);
        return await SendAsync(apiKey, plan.UserId, localKey, draft.Value, ct);
    }

    private async Task<Result<PushRoutineResult>> SendAsync(string apiKey, UserId userId, string localKey, HevyRoutineDraft draft, CancellationToken ct)
    {
        var now = _clock.NowUtc();
        var link = await _links.FindAsync(userId, HevyLinkKind.Routine, localKey, ct);

        if (link is not null)
        {
            var updated = await _hevy.UpdateRoutineAsync(apiKey, link.HevyId, draft, ct);
            if (updated.Ok)
            {
                link.Touch(now);
                _links.Update(link);
                await _unitOfWork.SaveChangesAsync(ct);
                return Result.Success(new PushRoutineResult(link.HevyId, draft.Title, PushRoutineResult.Updated));
            }

            if (updated.Error!.Kind != HevyErrorKind.NotFound)
            {
                return updated.Error.ToResult<PushRoutineResult>();
            }

            // The routine was deleted in Hevy: create it again and point the link at the new one.
            _logger.LogInformation("Hevy routine for {LocalKey} is gone; recreating", localKey);
        }

        var created = await _hevy.CreateRoutineAsync(apiKey, draft, ct);
        string routineId;
        if (created.Ok)
        {
            routineId = created.Value!.Id;
        }
        else if (created.Error!.Kind == HevyErrorKind.Unavailable)
        {
            // Review focus 3: the POST may have landed. Find it by title before anyone retries.
            var found = await FindRoutineByTitleAsync(apiKey, draft.Title, ct);
            if (found is null)
            {
                return created.Error.ToResult<PushRoutineResult>();
            }

            _logger.LogWarning("Hevy routine create timed out but '{Title}' exists; linking it", draft.Title);
            routineId = found.Id;
        }
        else
        {
            return created.Error.ToResult<PushRoutineResult>();
        }

        if (link is null)
        {
            await _links.AddAsync(HevyLink.Create(userId, HevyLinkKind.Routine, localKey, routineId, now), ct);
        }
        else
        {
            link.Relink(routineId, now);
            _links.Update(link);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success(new PushRoutineResult(routineId, draft.Title, PushRoutineResult.Created));
    }

    private async Task<HevyRoutine?> FindRoutineByTitleAsync(string apiKey, string title, CancellationToken ct)
    {
        for (var page = 1; page <= MaxLookupPages; page++)
        {
            var routines = await _hevy.GetRoutinesAsync(apiKey, page, ct);
            if (!routines.Ok)
            {
                return null;
            }

            var match = routines.Value!.Items.FirstOrDefault(r => string.Equals(r.Title, title, StringComparison.Ordinal));
            if (match is not null)
            {
                return match;
            }

            if (!routines.Value.HasMore)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>The plan's Hevy folder id: linked, else found by title, else created. Null only when Hevy has no folders API access.</summary>
    private async Task<Result<int?>> EnsureFolderAsync(string apiKey, TrainingPlan plan, CancellationToken ct)
    {
        var key = HevyLocalKeys.Folder(plan.Id);
        var link = await _links.FindAsync(plan.UserId, HevyLinkKind.Folder, key, ct);
        if (link is not null && int.TryParse(link.HevyId, out var linked))
        {
            return Result.Success<int?>(linked);
        }

        for (var page = 1; page <= MaxLookupPages; page++)
        {
            var folders = await _hevy.GetRoutineFoldersAsync(apiKey, page, ct);
            if (!folders.Ok)
            {
                return folders.Error!.ToResult<int?>();
            }

            var match = folders.Value!.Items.FirstOrDefault(f => string.Equals(f.Title, plan.Name, StringComparison.Ordinal));
            if (match is not null)
            {
                return Result.Success<int?>(await LinkFolderAsync(plan, key, match.Id, ct));
            }

            if (!folders.Value.HasMore)
            {
                break;
            }
        }

        var created = await _hevy.CreateRoutineFolderAsync(apiKey, plan.Name, ct);
        if (!created.Ok)
        {
            return created.Error!.ToResult<int?>();
        }

        return Result.Success<int?>(await LinkFolderAsync(plan, key, created.Value!.Id, ct));
    }

    private async Task<int> LinkFolderAsync(TrainingPlan plan, string key, int folderId, CancellationToken ct)
    {
        await _links.AddAsync(HevyLink.Create(plan.UserId, HevyLinkKind.Folder, key, folderId.ToString(), _clock.NowUtc()), ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return folderId;
    }

    private async Task<Result<HevyRoutineDraft>> BuildLiftAsync(string apiKey, TrainingPlan plan, ScheduledSession session, int? folderId, CancellationToken ct)
    {
        if (plan.LinkedWorkoutId is not { } workoutId)
        {
            return Result.Failure<HevyRoutineDraft>("The plan has no linked A2S2 workout to push.", ErrorCode.ValidationFailed);
        }

        var workout = await _workouts.GetByIdAsync(workoutId, ct);
        if (workout is null)
        {
            return Result.Failure<HevyRoutineDraft>("The plan's linked workout was not found.", ErrorCode.NotFound);
        }

        var day = session.LiftDay!.Value;
        var exerciseTemplates = new Dictionary<ExerciseId, string>();
        foreach (var exercise in workout.GetExercisesForDay(day))
        {
            var template = await _catalogue.ForExerciseAsync(apiKey, plan.UserId, exercise, ct);
            if (!template.Ok)
            {
                return template.Error!.ToResult<HevyRoutineDraft>();
            }

            exerciseTemplates[exercise.Id] = template.Value!;
        }

        if (exerciseTemplates.Count == 0)
        {
            return Result.Failure<HevyRoutineDraft>($"No exercises are assigned to {day}.", ErrorCode.ValidationFailed);
        }

        var warmUp = await WarmUpRoutineAsync(plan.UserId, ct);
        var drillTemplates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (warmUp is not null)
        {
            var drills = await DrillTemplatesAsync(apiKey, plan.UserId, warmUp, drillTemplates, ct);
            if (drills.IsFailure)
            {
                return PlanAccess.Fail<HevyRoutineDraft>(drills);
            }
        }

        var fromBrief = string.Equals(plan.Name, CutTo85Nov2026.Name, StringComparison.Ordinal);
        var title = RoutineBuilder.LiftTitle(plan.Name, workout.CurrentWeek, session.Label);
        var draft = RoutineBuilder.Lift(
            title,
            folderId,
            workout,
            day,
            _program.GetWeekParameters(workout.CurrentWeek),
            warmUp,
            drillTemplates,
            exerciseTemplates,
            fromBrief ? CutTo85Nov2026.Supersets() : Array.Empty<SupersetSpec>(),
            fromBrief ? CutTo85Nov2026.ExerciseNotes() : new Dictionary<string, string>());

        return Result.Success(draft);
    }

    private async Task<Result<HevyRoutineDraft>> BuildCardioAsync(string apiKey, TrainingPlan plan, ScheduledSession session, int? folderId, CancellationToken ct)
    {
        if (session.CardioProtocolId is not { } protocolId)
        {
            return Result.Failure<HevyRoutineDraft>("This cardio slot has no protocol; choose one before pushing it to Hevy.", ErrorCode.ValidationFailed);
        }

        var protocol = await _protocols.GetByIdAsync(protocolId, ct);
        if (protocol is null)
        {
            return Result.Failure<HevyRoutineDraft>("Cardio protocol not found.", ErrorCode.NotFound);
        }

        var template = await _catalogue.ForCardioAsync(apiKey, plan.UserId, protocol.Kind, ct);
        if (!template.Ok)
        {
            return template.Error!.ToResult<HevyRoutineDraft>();
        }

        return Result.Success(RoutineBuilder.Cardio(RoutineBuilder.SlotTitle(plan.Name, session.Label), folderId, protocol, template.Value!));
    }

    private async Task<Result<HevyRoutineDraft>> BuildMobilityAsync(string apiKey, TrainingPlan plan, ScheduledSession session, int? folderId, CancellationToken ct)
    {
        if (session.MobilityRoutineId is not { } routineId)
        {
            return Result.Failure<HevyRoutineDraft>("This mobility slot has no routine; choose one before pushing it to Hevy.", ErrorCode.ValidationFailed);
        }

        var routine = await _routines.GetByIdAsync(routineId, ct);
        if (routine is null)
        {
            return Result.Failure<HevyRoutineDraft>("Mobility routine not found.", ErrorCode.NotFound);
        }

        var drillTemplates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var drills = await DrillTemplatesAsync(apiKey, plan.UserId, routine, drillTemplates, ct);
        if (drills.IsFailure)
        {
            return PlanAccess.Fail<HevyRoutineDraft>(drills);
        }

        return Result.Success(RoutineBuilder.Mobility(RoutineBuilder.SlotTitle(plan.Name, session.Label), folderId, routine, drillTemplates));
    }

    private async Task<Result> DrillTemplatesAsync(string apiKey, UserId userId, MobilityRoutine routine, Dictionary<string, string> into, CancellationToken ct)
    {
        foreach (var drill in routine.Drills)
        {
            var template = await _catalogue.ForDrillAsync(apiKey, userId, drill, ct);
            if (!template.Ok)
            {
                return Result.Failure(template.Error!.Message, template.Error.ToErrorCode());
            }

            into[drill.Name] = template.Value!;
        }

        return Result.Success();
    }

    /// <summary>Mobility A (the brief's warm-up), or null when the user has no routine by that name.</summary>
    private async Task<MobilityRoutine?> WarmUpRoutineAsync(UserId userId, CancellationToken ct)
    {
        var routines = await _routines.ListForUserAsync(userId, ct);
        return routines.FirstOrDefault(r => string.Equals(r.Name, CutTo85Nov2026.MobilityAName, StringComparison.OrdinalIgnoreCase));
    }
}
