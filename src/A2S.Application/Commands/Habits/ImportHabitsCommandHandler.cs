using A2S.Application.Common;
using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using MediatR;

namespace A2S.Application.Commands.Habits;

public sealed class ImportHabitsCommandHandler : IRequestHandler<ImportHabitsCommand, Result<ImportResult>>
{
    private readonly IHabitRepository _habits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ILocalClock _clock;

    public ImportHabitsCommandHandler(
        IHabitRepository habits,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ILocalClock clock)
    {
        _habits = habits ?? throw new ArgumentNullException(nameof(habits));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Result<ImportResult>> Handle(ImportHabitsCommand request, CancellationToken cancellationToken)
    {
        var user = HabitAccess.RequireUser(_currentUser);
        if (user.IsFailure)
        {
            return HabitAccess.Fail<ImportResult>(user);
        }

        var userId = user.Value;
        var now = _clock.NowUtc();
        var existing = (await _habits.ListForUserAsync(userId, includeArchived: true, cancellationToken))
            .Where(h => h.ExternalKey is not null)
            .ToDictionary(h => h.ExternalKey!, StringComparer.OrdinalIgnoreCase);

        var allLogs = request.Habits.SelectMany(h => h.Logs).ToList();
        var storedLogs = allLogs.Count == 0
            ? []
            : await _habits.GetLogsAsync(userId, allLogs.Min(l => l.Date), allLogs.Max(l => l.Date), cancellationToken);
        var storedByHabitAndDate = storedLogs.ToDictionary(l => (l.HabitId, l.Date));

        int created = 0, matched = 0, written = 0, unchanged = 0;

        try
        {
            foreach (var import in request.Habits)
            {
                var key = import.ExternalKey.Trim();
                if (!existing.TryGetValue(key, out var habit))
                {
                    habit = HabitDefinition.Create(
                        userId, import.Name, import.Type, import.Target, import.Minimum, import.Unit, import.Order,
                        externalKey: key);
                    await _habits.AddAsync(habit, cancellationToken);
                    existing[key] = habit;
                    created++;
                }
                else
                {
                    matched++;
                }

                // Last value wins when the same date appears twice in one import.
                var byDate = import.Logs
                    .GroupBy(l => l.Date)
                    .ToDictionary(g => g.Key, g => g.Last().Value);

                foreach (var (date, value) in byDate)
                {
                    if (storedByHabitAndDate.TryGetValue((habit.Id, date), out var stored))
                    {
                        if (stored.Value == value)
                        {
                            unchanged++;
                            continue;
                        }

                        stored.SetValue(value, now);
                        _habits.UpdateLog(stored);
                        written++;
                        continue;
                    }

                    var log = HabitLog.Create(habit.Id, userId, date, value, now);
                    await _habits.AddLogAsync(log, cancellationToken);
                    storedByHabitAndDate[(habit.Id, date)] = log;
                    written++;
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success(new ImportResult(created, matched, written, unchanged));
        }
        catch (BusinessRuleViolationException ex)
        {
            return Result.Failure<ImportResult>(ex.Message, ErrorCode.DomainRuleViolation);
        }
    }
}
