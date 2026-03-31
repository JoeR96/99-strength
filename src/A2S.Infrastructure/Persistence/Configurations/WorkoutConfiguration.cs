using System.Text.Json;
using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Workout aggregate root.
/// </summary>
public class WorkoutConfiguration : IEntityTypeConfiguration<Workout>
{
    public void Configure(EntityTypeBuilder<Workout> builder)
    {
        builder.ToTable("Workouts");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id)
            .HasConversion(
                id => id.Value,
                value => new WorkoutId(value))
            .ValueGeneratedNever();

        builder.Property(w => w.Name)
            .IsRequired()
            .HasMaxLength(200);

        // PostgreSQL optimistic concurrency via xmin system column
        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.Property(w => w.UserId)
            .HasConversion(
                id => id.Value,
                value => new UserId(value))
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(w => w.Variant)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(w => w.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(w => w.CurrentWeek)
            .IsRequired();

        builder.Property(w => w.CurrentBlock)
            .IsRequired();

        builder.Property(w => w.CreatedAt)
            .IsRequired();

        builder.Property(w => w.StartedAt);

        builder.Property(w => w.CompletedAt);

        // BlockSequence as JSON column
        var blockSequenceConverter = new ValueConverter<List<int>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => string.IsNullOrEmpty(v)
                ? new List<int> { 1, 2, 3 }
                : JsonSerializer.Deserialize<List<int>>(v, (JsonSerializerOptions?)null) ?? new List<int> { 1, 2, 3 }
        );

        builder.Property<List<int>>("_blockSequence")
            .HasColumnName("BlockSequence")
            .HasColumnType("jsonb")
            .HasConversion(blockSequenceConverter)
            .HasDefaultValueSql("'[1,2,3]'::jsonb")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Exercises relationship
        builder.HasMany(w => w.Exercises)
            .WithOne()
            .HasForeignKey("WorkoutId")
            .OnDelete(DeleteBehavior.Cascade);

        // Ignore domain events (not persisted)
        builder.Ignore(w => w.DomainEvents);

        // AuditEntries as owned complex type (JSON column)
        builder.Navigation(w => w.AuditEntries)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(w => w.AuditEntries, audit =>
        {
            audit.ToJson();
            audit.Property(a => a.OccurredAt);
            audit.Property(a => a.Type).HasConversion<string>();
            audit.Property(a => a.ExerciseId)
                .HasConversion(
                    new ValueConverter<ExerciseId?, Guid?>(
                        id => id.HasValue ? id.Value.Value : null,
                        value => value.HasValue ? new ExerciseId(value.Value) : null));
            audit.Property(a => a.ExerciseName).HasMaxLength(200);
            audit.Property(a => a.WeekNumber);
            audit.Property(a => a.DayNumber);
            audit.Property(a => a.OldValue);
            audit.Property(a => a.NewValue);
            audit.Property(a => a.Reason).HasMaxLength(500);
        });

        // CompletedActivities as owned complex type (JSON column)
        // EF Core 9 handles nested collections in JSON automatically
        // Use backing field to allow EF Core to populate the private List<T>
        builder.Navigation(w => w.CompletedActivities)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(w => w.CompletedActivities, activity =>
        {
            activity.ToJson();
            activity.Property(a => a.Day).HasConversion<string>();
            activity.Property(a => a.WeekNumber);
            activity.Property(a => a.BlockNumber);
            activity.Property(a => a.CompletedAt);

            // Include Performances in JSON serialization for history tracking
            activity.OwnsMany(a => a.Performances, perf =>
            {
                perf.Property(p => p.ExerciseId)
                    .HasConversion(
                        id => id.Value,
                        value => new ExerciseId(value));
                perf.Property(p => p.CompletedAt);
                perf.OwnsMany(p => p.CompletedSets, set =>
                {
                    set.Property(s => s.SetNumber);
                    set.Property(s => s.ActualReps);
                    set.Property(s => s.WasAmrap);
                    set.OwnsOne(s => s.Weight, w =>
                    {
                        w.Property(x => x.Value);
                        w.Property(x => x.Unit).HasConversion<string>();
                    });
                });
                perf.OwnsMany(p => p.PlannedSets, set =>
                {
                    set.Property(s => s.SetNumber);
                    set.Property(s => s.TargetReps);
                    set.Property(s => s.IsAmrap);
                    set.OwnsOne(s => s.Weight, w =>
                    {
                        w.Property(x => x.Value);
                        w.Property(x => x.Unit).HasConversion<string>();
                    });
                });
            });

            // ProgressionSnapshots for undo capability
            activity.OwnsMany(a => a.ProgressionSnapshots, snap =>
            {
                snap.Property(s => s.ExerciseId)
                    .HasConversion(
                        id => id.Value,
                        value => new ExerciseId(value));
                snap.Property(s => s.ExerciseName).HasMaxLength(200);
                snap.Property(s => s.ProgressionType).HasMaxLength(50);
                snap.Property(s => s.ProgressionStateJson);
            });
        });

        // ArchivedActivities as owned complex type (JSON column)
        // Stores historical activities from completed program cycles
        builder.OwnsMany(w => w.ArchivedActivities, activity =>
        {
            activity.ToJson();
            activity.Property(a => a.Day).HasConversion<string>();
            activity.Property(a => a.WeekNumber);
            activity.Property(a => a.BlockNumber);
            activity.Property(a => a.CompletedAt);

            activity.OwnsMany(a => a.Performances, perf =>
            {
                perf.Property(p => p.ExerciseId)
                    .HasConversion(
                        id => id.Value,
                        value => new ExerciseId(value));
                perf.Property(p => p.CompletedAt);
                perf.OwnsMany(p => p.CompletedSets, set =>
                {
                    set.Property(s => s.SetNumber);
                    set.Property(s => s.ActualReps);
                    set.Property(s => s.WasAmrap);
                    set.OwnsOne(s => s.Weight, w =>
                    {
                        w.Property(x => x.Value);
                        w.Property(x => x.Unit).HasConversion<string>();
                    });
                });
                perf.OwnsMany(p => p.PlannedSets, set =>
                {
                    set.Property(s => s.SetNumber);
                    set.Property(s => s.TargetReps);
                    set.Property(s => s.IsAmrap);
                    set.OwnsOne(s => s.Weight, w =>
                    {
                        w.Property(x => x.Value);
                        w.Property(x => x.Unit).HasConversion<string>();
                    });
                });
            });

            activity.OwnsMany(a => a.ProgressionSnapshots, snap =>
            {
                snap.Property(s => s.ExerciseId)
                    .HasConversion(
                        id => id.Value,
                        value => new ExerciseId(value));
                snap.Property(s => s.ExerciseName).HasMaxLength(200);
                snap.Property(s => s.ProgressionType).HasMaxLength(50);
                snap.Property(s => s.ProgressionStateJson);
            });
        });

        builder.Navigation(w => w.ArchivedActivities)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
