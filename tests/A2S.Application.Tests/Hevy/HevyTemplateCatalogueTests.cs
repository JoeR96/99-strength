using A2S.Application.Hevy;
using A2S.Application.Interfaces.Hevy;
using A2S.Application.Services;
using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.Repositories;
using A2S.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace A2S.Application.Tests.Hevy;

public class HevyTemplateCatalogueTests
{
    private const string Key = "test";
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly IHevyClient _hevy = Substitute.For<IHevyClient>();
    private readonly InMemoryLinks _links = new();
    private readonly IExerciseLibraryProvider _library = Substitute.For<IExerciseLibraryProvider>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly HevyTemplateCatalogue _catalogue;

    public HevyTemplateCatalogueTests()
    {
        _catalogue = new HevyTemplateCatalogue(_hevy, _links, _library, _unitOfWork, new LocalClock(new FakeTimeProvider(Now)));
        _hevy.CreateExerciseTemplateAsync(Key, Arg.Any<HevyExerciseTemplateDraft>(), Arg.Any<CancellationToken>())
            .Returns(call => HevyResult<HevyExerciseTemplate>.Success(new HevyExerciseTemplate
            {
                Id = "T-" + call.Arg<HevyExerciseTemplateDraft>().Title.Replace(' ', '-'),
                Title = call.Arg<HevyExerciseTemplateDraft>().Title,
                IsCustom = true
            }));
    }

    [Fact]
    public async Task LibraryExercise_UsesItsOwnHevyId_WithoutCallingHevy()
    {
        var bench = Exercise.CreateWithLinearProgression("Bench Press (Barbell)", ExerciseCategory.MainLift, EquipmentType.Barbell,
            DayNumber.Day1, 1, "79D0BB3A", TrainingMax.Create(100m, WeightUnit.Kilograms));

        var result = await _catalogue.ForExerciseAsync(Key, Joe, bench);

        result.Require().Should().Be("79D0BB3A");
        await _hevy.DidNotReceive().CreateExerciseTemplateAsync(Arg.Any<string>(), Arg.Any<HevyExerciseTemplateDraft>(), Arg.Any<CancellationToken>());
        _links.All.Should().BeEmpty();
    }

    [Fact]
    public async Task CustomExercise_IsCreatedOnce_WithTheLibraryMuscleGroup_AndReusedFromTheLink()
    {
        _library.GetByName("Seal Row (Barbell)").Returns(new ExerciseTemplate("Seal Row (Barbell)", EquipmentType.Barbell, MuscleGroup: "upper_back"));
        var sealRow = Exercise.CreateWithRepsPerSetProgression("Seal Row (Barbell)", ExerciseCategory.Accessory, EquipmentType.Barbell,
            DayNumber.Day3, 4, "custom:seal-row-barbell", RepRange.Create(10, 10), 3, 5, false, Weight.Create(40m, WeightUnit.Kilograms));

        var first = await _catalogue.ForExerciseAsync(Key, Joe, sealRow);
        var second = await _catalogue.ForExerciseAsync(Key, Joe, sealRow);

        first.Require().Should().Be("T-Seal-Row-(Barbell)");
        second.Require().Should().Be(first.Value);
        await _hevy.Received(1).CreateExerciseTemplateAsync(Key,
            Arg.Is<HevyExerciseTemplateDraft>(d => d.Title == "Seal Row (Barbell)" && d.ExerciseType == HevyExerciseTypes.WeightReps && d.PrimaryMuscleGroup == "upper_back"),
            Arg.Any<CancellationToken>());
        _links.All.Should().ContainSingle(l => l.Kind == HevyLinkKind.Template && l.LocalKey == "exercise:seal-row-barbell" && l.HevyId == first.Value);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BodyweightCustomExercise_WithUnknownMuscleGroup_IsBodyweightRepsOther()
    {
        _library.GetByName(Arg.Any<string>()).Returns((ExerciseTemplate?)null);
        var plank = Exercise.CreateWithRepsPerSetProgression("Copenhagen Plank (Short Lever)", ExerciseCategory.Accessory, EquipmentType.Bodyweight,
            DayNumber.Day4, 4, "custom:copenhagen-plank-short-lever", RepRange.Create(20, 20), 3, 3, true, Weight.Create(0m, WeightUnit.Kilograms));

        await _catalogue.ForExerciseAsync(Key, Joe, plank);

        await _hevy.Received(1).CreateExerciseTemplateAsync(Key,
            Arg.Is<HevyExerciseTemplateDraft>(d => d.ExerciseType == HevyExerciseTypes.BodyweightReps && d.PrimaryMuscleGroup == "other"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Drill_PicksDurationForSeconds_AndRepsOnlyOtherwise_AndAnExistingLinkIsReused()
    {
        _links.Seed(HevyLink.Create(Joe, HevyLinkKind.Template, HevyLocalKeys.DrillTemplate("Open books"), "T-existing", Now));
        var openBooks = new MobilityDrill("Open books", 2, DrillDose.Reps(5), SideBias.Left, null, null, true);
        var sidePlank = new MobilityDrill("Side plank", 3, DrillDose.Seconds(30), SideBias.Left, null, null, true);
        var breathing = new MobilityDrill("90/90 breathing", 2, DrillDose.Breaths(5), SideBias.None, null, null, false);

        (await _catalogue.ForDrillAsync(Key, Joe, openBooks)).Require().Should().Be("T-existing");
        (await _catalogue.ForDrillAsync(Key, Joe, sidePlank)).Require().Should().Be("T-Side-plank");
        (await _catalogue.ForDrillAsync(Key, Joe, breathing)).Require().Should().Be("T-90/90-breathing");

        await _hevy.Received(1).CreateExerciseTemplateAsync(Key,
            Arg.Is<HevyExerciseTemplateDraft>(d => d.Title == "Side plank" && d.ExerciseType == HevyExerciseTypes.Duration && d.PrimaryMuscleGroup == "full_body"),
            Arg.Any<CancellationToken>());
        await _hevy.Received(1).CreateExerciseTemplateAsync(Key,
            Arg.Is<HevyExerciseTemplateDraft>(d => d.Title == "90/90 breathing" && d.ExerciseType == HevyExerciseTypes.RepsOnly),
            Arg.Any<CancellationToken>());
        _links.All.Select(l => l.LocalKey).Should().BeEquivalentTo("drill:open-books", "drill:side-plank", "drill:90/90-breathing");
    }

    [Fact]
    public async Task Cardio_OneTemplatePerKind_LissIsDistanceDuration()
    {
        (await _catalogue.ForCardioAsync(Key, Joe, CardioProtocolKind.NorwegianFourByFour)).Require().Should().Be("T-Norwegian-4×4");
        (await _catalogue.ForCardioAsync(Key, Joe, CardioProtocolKind.NorwegianFourByFour)).Require().Should().Be("T-Norwegian-4×4");
        (await _catalogue.ForCardioAsync(Key, Joe, CardioProtocolKind.Liss)).Require().Should().Be("T-LISS");

        await _hevy.Received(2).CreateExerciseTemplateAsync(Key, Arg.Any<HevyExerciseTemplateDraft>(), Arg.Any<CancellationToken>());
        await _hevy.Received(1).CreateExerciseTemplateAsync(Key,
            Arg.Is<HevyExerciseTemplateDraft>(d => d.ExerciseType == HevyExerciseTypes.DistanceDuration && d.PrimaryMuscleGroup == "cardio"),
            Arg.Any<CancellationToken>());
        _links.All.Select(l => l.LocalKey).Should().BeEquivalentTo("cardio:NorwegianFourByFour", "cardio:Liss");
    }

    [Fact]
    public async Task HevyFailure_IsReturned_AndNothingIsLinked()
    {
        _hevy.CreateExerciseTemplateAsync(Key, Arg.Any<HevyExerciseTemplateDraft>(), Arg.Any<CancellationToken>())
            .Returns(HevyResult<HevyExerciseTemplate>.Failure(HevyError.ProNeeded()));

        var result = await _catalogue.ForCardioAsync(Key, Joe, CardioProtocolKind.Liss);

        result.Ok.Should().BeFalse();
        result.Error!.Kind.Should().Be(HevyErrorKind.ProNeeded);
        _links.All.Should().BeEmpty();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TemplateIds_MapHevyIdsBackToLocalKeys()
    {
        _links.Seed(HevyLink.Create(Joe, HevyLinkKind.Template, "drill:open-books", "T-1", Now));
        _links.Seed(HevyLink.Create(Joe, HevyLinkKind.Template, "cardio:Liss", "T-2", Now));
        _links.Seed(HevyLink.Create(Joe, HevyLinkKind.Routine, "plan:x", "R-1", Now));

        var map = await _catalogue.TemplateIdsAsync(Joe);

        map.Should().BeEquivalentTo(new Dictionary<string, string> { ["T-1"] = "drill:open-books", ["T-2"] = "cardio:Liss" });
    }
}

/// <summary>A list-backed <see cref="IHevyLinkRepository"/> for handler tests.</summary>
public sealed class InMemoryLinks : IHevyLinkRepository
{
    private readonly List<HevyLink> _links = new();

    public IReadOnlyList<HevyLink> All => _links;

    public void Seed(HevyLink link) => _links.Add(link);

    public Task<HevyLink?> GetByIdAsync(HevyLinkId id, CancellationToken ct = default) =>
        Task.FromResult(_links.FirstOrDefault(l => l.Id == id));

    public Task AddAsync(HevyLink entity, CancellationToken ct = default)
    {
        _links.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(HevyLink entity)
    {
    }

    public void Remove(HevyLink entity) => _links.Remove(entity);

    public Task<HevyLink?> FindAsync(UserId userId, HevyLinkKind kind, string localKey, CancellationToken ct = default) =>
        Task.FromResult(_links.FirstOrDefault(l => l.UserId == userId && l.Kind == kind && l.LocalKey == localKey.Trim()));

    public Task<HevyLink?> FindByHevyIdAsync(UserId userId, HevyLinkKind kind, string hevyId, CancellationToken ct = default) =>
        Task.FromResult(_links.FirstOrDefault(l => l.UserId == userId && l.Kind == kind && l.HevyId == hevyId.Trim()));

    public Task<IReadOnlyList<HevyLink>> ListAsync(UserId userId, HevyLinkKind kind, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<HevyLink>>(_links.Where(l => l.UserId == userId && l.Kind == kind).ToList());
}
