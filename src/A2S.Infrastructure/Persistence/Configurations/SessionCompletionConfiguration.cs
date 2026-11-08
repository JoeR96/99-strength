using System.Text.Json;
using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for SessionCompletion, a child table of TrainingPlans. The cardio log is
/// owned JSON, drill ticks are a jsonb array, and a Hevy workout id is unique within a plan.
/// </summary>
public class SessionCompletionConfiguration : IEntityTypeConfiguration<SessionCompletion>
{
    public void Configure(EntityTypeBuilder<SessionCompletion> builder)
    {
        builder.ToTable("SessionCompletions");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => new SessionCompletionId(value))
            .ValueGeneratedNever();

        builder.Property<TrainingPlanId>("TrainingPlanId")
            .HasConversion(id => id.Value, value => new TrainingPlanId(value))
            .IsRequired();

        builder.Property(c => c.Date).IsRequired();

        builder.Property(c => c.Kind)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.LiftDay)
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(c => c.CardioProtocolId)
            .HasConversion(new ValueConverter<CardioProtocolId?, Guid?>(
                id => id.HasValue ? id.Value.Value : null,
                value => value.HasValue ? new CardioProtocolId(value.Value) : null));

        builder.Property(c => c.MobilityRoutineId)
            .HasConversion(new ValueConverter<MobilityRoutineId?, Guid?>(
                id => id.HasValue ? id.Value.Value : null,
                value => value.HasValue ? new MobilityRoutineId(value.Value) : null));

        builder.Property(c => c.Source)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.HevyWorkoutId).HasMaxLength(100);

        builder.Property(c => c.RecordedAtUtc).IsRequired();

        builder.OwnsOne(c => c.Cardio, log =>
        {
            log.ToJson();
            log.Property(l => l.DurationSeconds);
            log.Property(l => l.AvgHeartRate);
            log.Property(l => l.Rpe);
            log.Property(l => l.DistanceMeters);
            log.Property(l => l.Notes).HasMaxLength(1000);
        });

        var drillTicksConverter = new ValueConverter<List<bool>?, string?>(
            v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => v == null ? null : JsonSerializer.Deserialize<List<bool>>(v, (JsonSerializerOptions?)null));

        var drillTicksComparer = new ValueComparer<List<bool>?>(
            (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
            v => v == null ? 0 : v.Aggregate(17, (hash, tick) => hash * 31 + (tick ? 1 : 0)),
            v => v == null ? null : v.ToList());

        builder.Property<List<bool>?>("_drillTicks")
            .HasColumnName("DrillTicks")
            .HasColumnType("jsonb")
            .HasConversion(drillTicksConverter, drillTicksComparer)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(c => c.DrillTicks);

        builder.HasIndex("TrainingPlanId", nameof(SessionCompletion.HevyWorkoutId))
            .IsUnique()
            .HasFilter("\"HevyWorkoutId\" IS NOT NULL");

        builder.HasIndex("TrainingPlanId", nameof(SessionCompletion.Date));
    }
}
