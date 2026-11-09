using A2S.Application.Commands.Habits;
using A2S.Application.Common;
using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Commands.Habits;

/// <summary>
/// Uses an in-memory fake repository so a second import sees what the first one wrote.
/// </summary>
public class ImportHabitsCommandHandlerTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeHabitRepository _habits = new();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ILocalClock _clock = Substitute.For<ILocalClock>();
    private readonly ImportHabitsCommandHandler _handler;

    private static readonly ImportHabitsCommand TerranImport = new(
    [
        new ImportHabitDto("terran:walks", "Walks", HabitType.Counter, 3, 1, null, 1,
        [
            new ImportHabitLogDto(new DateOnly(2026, 11, 7), 2),
            new ImportHabitLogDto(new DateOnly(2026, 11, 8), 3)
        ]),
        new ImportHabitDto("terran:creatine", "Creatine", HabitType.Checkbox, 1, 1, null, 4,
        [
            new ImportHabitLogDto(new DateOnly(2026, 11, 8), 1)
        ])
    ]);

    public ImportHabitsCommandHandlerTests()
    {
        _currentUser.UserId.Returns(Joe.Value);
        _clock.NowUtc().Returns(Now);
        _handler = new ImportHabitsCommandHandler(_habits, _unitOfWork, _currentUser, _clock);
    }

    [Fact]
    public async Task FirstImport_CreatesHabitsAndWritesLogs()
    {
        var result = await _handler.Handle(TerranImport, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new ImportResult(HabitsCreated: 2, HabitsMatched: 0, LogsWritten: 3, LogsUnchanged: 0));
        _habits.Habits.Should().HaveCount(2);
        _habits.Habits.Select(h => h.ExternalKey).Should().BeEquivalentTo("terran:walks", "terran:creatine");
        _habits.Logs.Should().HaveCount(3);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SecondIdenticalImport_WritesNothing()
    {
        await _handler.Handle(TerranImport, CancellationToken.None);

        var result = await _handler.Handle(TerranImport, CancellationToken.None);

        result.Value.Should().Be(new ImportResult(HabitsCreated: 0, HabitsMatched: 2, LogsWritten: 0, LogsUnchanged: 3));
        _habits.Habits.Should().HaveCount(2);
        _habits.Logs.Should().HaveCount(3);
    }

    [Fact]
    public async Task ChangedValue_IsRewritten()
    {
        await _handler.Handle(TerranImport, CancellationToken.None);
        var corrected = new ImportHabitsCommand(
        [
            new ImportHabitDto("terran:walks", "Walks", HabitType.Counter, 3, 1, null, 1,
            [
                new ImportHabitLogDto(new DateOnly(2026, 11, 7), 2),
                new ImportHabitLogDto(new DateOnly(2026, 11, 8), 1)
            ])
        ]);

        var result = await _handler.Handle(corrected, CancellationToken.None);

        result.Value.Should().Be(new ImportResult(0, 1, LogsWritten: 1, LogsUnchanged: 1));
        _habits.Logs.Single(l => l.Date == new DateOnly(2026, 11, 8) && l.HabitId == _habits.Habits.Single(h => h.ExternalKey == "terran:walks").Id)
            .Value.Should().Be(1);
    }

    [Fact]
    public async Task Unauthenticated_Fails()
    {
        _currentUser.UserId.Returns((string?)null);

        var result = await _handler.Handle(TerranImport, CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.Unauthenticated);
    }

    private sealed class FakeHabitRepository : IHabitRepository
    {
        public List<HabitDefinition> Habits { get; } = [];
        public List<HabitLog> Logs { get; } = [];

        public Task<HabitDefinition?> GetByIdAsync(HabitId id, CancellationToken ct = default) =>
            Task.FromResult(Habits.FirstOrDefault(h => h.Id == id));

        public Task AddAsync(HabitDefinition entity, CancellationToken ct = default)
        {
            Habits.Add(entity);
            return Task.CompletedTask;
        }

        public void Update(HabitDefinition entity) { }

        public void Remove(HabitDefinition entity) => Habits.Remove(entity);

        public Task<IReadOnlyList<HabitDefinition>> ListForUserAsync(UserId userId, bool includeArchived, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<HabitDefinition>>(Habits.Where(h => h.UserId == userId && (includeArchived || !h.Archived)).ToList());

        public Task<IReadOnlyList<HabitLog>> GetLogsAsync(UserId userId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<HabitLog>>(Logs.Where(l => l.UserId == userId && l.Date >= from && l.Date <= to).ToList());

        public Task<HabitLog?> GetLogAsync(HabitId habitId, DateOnly date, CancellationToken ct = default) =>
            Task.FromResult(Logs.FirstOrDefault(l => l.HabitId == habitId && l.Date == date));

        public Task AddLogAsync(HabitLog log, CancellationToken ct = default)
        {
            Logs.Add(log);
            return Task.CompletedTask;
        }

        public void UpdateLog(HabitLog log) { }
    }
}
