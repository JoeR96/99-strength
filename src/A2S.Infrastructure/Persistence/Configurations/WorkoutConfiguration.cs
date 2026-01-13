using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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

        // Exercises relationship
        builder.HasMany(w => w.Exercises)
            .WithOne()
            .HasForeignKey("WorkoutId")
            .OnDelete(DeleteBehavior.Cascade);

        // Ignore domain events (not persisted)
        builder.Ignore(w => w.DomainEvents);

        // CompletedActivities as owned complex type (JSON column)
        builder.OwnsMany(w => w.CompletedActivities, activity =>
        {
            activity.ToJson();

            // Configure complex type mapping for WorkoutActivity
            activity.Property(a => a.Day)
                .HasConversion<string>(); // Store DayNumber as string

            activity.Property(a => a.WeekNumber);
            activity.Property(a => a.BlockNumber);
            activity.Property(a => a.CompletedAt);

            // Performances collection within WorkoutActivity
            // EF Core will handle nested collections in JSON automatically
        });
    }
}
