using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Exercise entity.
/// </summary>
public class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("Exercises");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasConversion(
                id => id.Value,
                value => new ExerciseId(value))
            .ValueGeneratedNever();

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Category)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(e => e.Equipment)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(e => e.AssignedDay)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(e => e.OrderInDay)
            .IsRequired();

        builder.Property(e => e.HevyExerciseTemplateId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property<WorkoutId>("WorkoutId")
            .HasConversion(
                id => id.Value,
                value => new WorkoutId(value))
            .IsRequired();

        // ExerciseProgression relationship (one-to-one)
        builder.HasOne(e => e.Progression)
            .WithOne()
            .HasForeignKey<ExerciseProgression>("ExerciseId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
