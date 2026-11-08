using A2S.Application.Commands.Habits;
using A2S.Application.Common;
using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace A2S.Application.Tests.Commands.Habits;

public class HabitDefinitionCommandHandlerTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly UserId Other = new("user_other");

    private readonly IHabitRepository _habits = Substitute.For<IHabitRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    public HabitDefinitionCommandHandlerTests()
    {
        _currentUser.UserId.Returns(Joe.Value);
    }

    [Fact]
    public async Task Create_PersistsAndReturnsDto()
    {
        var handler = new CreateHabitCommandHandler(_habits, _unitOfWork, _currentUser);

        var result = await handler.Handle(new CreateHabitCommand("Water", HabitType.Counter, 6, 4, "bottles", 0, "droplet"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Water");
        result.Value.Target.Should().Be(6);
        result.Value.Minimum.Should().Be(4);
        result.Value.Icon.Should().Be("droplet");
        await _habits.Received(1).AddAsync(Arg.Is<HabitDefinition>(h => h.UserId == Joe), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_Unauthenticated_Fails()
    {
        _currentUser.UserId.Returns((string?)null);
        var handler = new CreateHabitCommandHandler(_habits, _unitOfWork, _currentUser);

        var result = await handler.Handle(new CreateHabitCommand("Water", HabitType.Counter, 6, 4, null, 0), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.Unauthenticated);
        await _habits.DidNotReceive().AddAsync(Arg.Any<HabitDefinition>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_MinimumAboveTarget_IsADomainRuleViolation()
    {
        var handler = new CreateHabitCommandHandler(_habits, _unitOfWork, _currentUser);

        var result = await handler.Handle(new CreateHabitCommand("Water", HabitType.Counter, 2, 4, null, 0), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.DomainRuleViolation);
    }

    [Fact]
    public async Task Update_ChangesEverythingButType()
    {
        var water = HabitDefinition.Create(Joe, "Water", HabitType.Counter, 6, 4, "bottles", 0);
        _habits.GetByIdAsync(water.Id, Arg.Any<CancellationToken>()).Returns(water);
        var handler = new UpdateHabitCommandHandler(_habits, _unitOfWork, _currentUser);

        var result = await handler.Handle(new UpdateHabitCommand(water.Id.Value, "Water bottles", 8, 5, "x532ml", 2, "cup"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new { Name = "Water bottles", Type = HabitType.Counter, Target = 8m, Minimum = 5m, Unit = "x532ml", Order = 2, Icon = "cup" });
        _habits.Received(1).Update(water);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_SomeoneElsesHabit_IsForbidden()
    {
        var theirs = HabitDefinition.Create(Other, "Water", HabitType.Counter, 6, 4, null, 0);
        _habits.GetByIdAsync(theirs.Id, Arg.Any<CancellationToken>()).Returns(theirs);
        var handler = new UpdateHabitCommandHandler(_habits, _unitOfWork, _currentUser);

        var result = await handler.Handle(new UpdateHabitCommand(theirs.Id.Value, "Mine now", 6, 4, null, 0, null), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.Unauthorized);
        theirs.Name.Should().Be("Water");
    }

    [Fact]
    public async Task Archive_ThenUnarchive_Flips()
    {
        var water = HabitDefinition.Create(Joe, "Water", HabitType.Counter, 6, 4, null, 0);
        _habits.GetByIdAsync(water.Id, Arg.Any<CancellationToken>()).Returns(water);

        var archive = await new ArchiveHabitCommandHandler(_habits, _unitOfWork, _currentUser)
            .Handle(new ArchiveHabitCommand(water.Id.Value), CancellationToken.None);
        var archived = water.Archived;
        var unarchive = await new UnarchiveHabitCommandHandler(_habits, _unitOfWork, _currentUser)
            .Handle(new UnarchiveHabitCommand(water.Id.Value), CancellationToken.None);

        archive.IsSuccess.Should().BeTrue();
        archived.Should().BeTrue();
        unarchive.IsSuccess.Should().BeTrue();
        water.Archived.Should().BeFalse();
        await _unitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Archive_UnknownHabit_IsNotFound()
    {
        var result = await new ArchiveHabitCommandHandler(_habits, _unitOfWork, _currentUser)
            .Handle(new ArchiveHabitCommand(Guid.Parse("99999999-9999-9999-9999-999999999999")), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCode.NotFound);
    }
}
