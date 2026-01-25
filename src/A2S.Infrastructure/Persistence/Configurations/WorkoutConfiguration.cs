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

        builder.Property(w => w.HevyRoutineFolderId)
            .HasMaxLength(100);

        // HevySyncedRoutines as JSON column with explicit value converter
        // This avoids Npgsql 8.0+ dynamic JSON serialization requirement
        var dictionaryConverter = new ValueConverter<Dictionary<string, string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => string.IsNullOrEmpty(v) ? new Dictionary<string, string>() : JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>()
        );

        builder.Property(w => w.HevySyncedRoutines)
            .HasColumnType("jsonb")
            .HasConversion(dictionaryConverter)
            .HasDefaultValueSql("'{}'::jsonb");

        // Exercises relationship
        builder.HasMany(w => w.Exercises)
            .WithOne()
            .HasForeignKey("WorkoutId")
            .OnDelete(DeleteBehavior.Cascade);

        // Ignore domain events (not persisted)
        builder.Ignore(w => w.DomainEvents);

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
        });
    }
}
