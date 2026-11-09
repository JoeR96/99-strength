using A2S.Application.Common;
using A2S.Application.Hevy;
using A2S.Application.Interfaces;
using A2S.Application.Interfaces.Hevy;
using A2S.Domain.Common;
using A2S.Domain.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace A2S.Application.Tests.Hevy;

public class MirrorWeighInToHevyTests
{
    private static readonly UserId Joe = new("user_joe");
    private static readonly DateOnly Mon16Nov = new(2026, 11, 16);
    private static readonly DateTimeOffset At = new(2026, 11, 16, 7, 0, 0, TimeSpan.Zero);

    private readonly IHevyKeyResolver _keys = Substitute.For<IHevyKeyResolver>();
    private readonly IHevyClient _hevy = Substitute.For<IHevyClient>();
    private readonly MirrorWeighInToHevy _handler;

    public MirrorWeighInToHevyTests()
    {
        _handler = new MirrorWeighInToHevy(_keys, _hevy, NullLogger<MirrorWeighInToHevy>.Instance);
        _keys.ForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns("test");
        _hevy.UpsertBodyMeasurementAsync("test", Mon16Nov, 94.3m, Arg.Any<CancellationToken>())
            .Returns(HevyResult<HevyBodyMeasurement>.Success(new HevyBodyMeasurement { Date = "2026-11-16", WeightKg = 94.3m }));
    }

    private static DomainEventNotification WeighIn() => new(new BodyWeightRecorded(Joe, Mon16Nov, 94.3m, At));

    [Fact]
    public async Task WithAKey_UpsertsTheMeasurementForTheDate()
    {
        await _handler.Handle(WeighIn(), CancellationToken.None);

        await _hevy.Received(1).UpsertBodyMeasurementAsync("test", Mon16Nov, 94.3m, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithoutAKey_DoesNothing()
    {
        _keys.ForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns((string?)null);

        await _handler.Handle(WeighIn(), CancellationToken.None);

        await _hevy.DidNotReceiveWithAnyArgs().UpsertBodyMeasurementAsync(default!, default, default, default);
    }

    [Fact]
    public async Task OtherEvents_AreIgnored()
    {
        await _handler.Handle(new DomainEventNotification(new TrainingPlanStarted(new TrainingPlanId(Guid.NewGuid()), Joe, Mon16Nov)), CancellationToken.None);

        await _keys.DidNotReceiveWithAnyArgs().ForUserAsync(default, default);
    }

    [Fact]
    public async Task AHevyFailure_OrAnException_NeverSurfaces()
    {
        _hevy.UpsertBodyMeasurementAsync("test", Mon16Nov, 94.3m, Arg.Any<CancellationToken>())
            .Returns(HevyResult<HevyBodyMeasurement>.Failure(HevyError.ProNeeded()));
        var failed = () => _handler.Handle(WeighIn(), CancellationToken.None);
        await failed.Should().NotThrowAsync();

        _keys.ForUserAsync(Joe, Arg.Any<CancellationToken>()).Returns<string?>(_ => throw new InvalidOperationException("key store down"));
        var threw = () => _handler.Handle(WeighIn(), CancellationToken.None);
        await threw.Should().NotThrowAsync();
    }
}
