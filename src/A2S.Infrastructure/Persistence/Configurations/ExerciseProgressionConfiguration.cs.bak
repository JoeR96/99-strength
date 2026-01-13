using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using A2S.Domain.Enums;
using A2S.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ExerciseProgression hierarchy (TPH - Table Per Hierarchy).
/// </summary>
public class ExerciseProgressionConfiguration : IEntityTypeConfiguration<ExerciseProgression>
{
    public void Configure(EntityTypeBuilder<ExerciseProgression> builder)
    {
        builder.ToTable("ExerciseProgressions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(
                id => id.Value,
                value => new ExerciseProgressionId(value))
            .ValueGeneratedNever();

        builder.Property(p => p.ProgressionType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property("ExerciseId")
            .IsRequired();

        // TPH discriminator
        builder.HasDiscriminator(p => p.ProgressionType)
            .HasValue<LinearProgressionStrategy>("Linear")
            .HasValue<RepsPerSetStrategy>("RepsPerSet");

        // LinearProgressionStrategy specific properties
        builder.OwnsOne<TrainingMax>("TrainingMax", tm =>
        {
            tm.Property(t => t.Value)
                .HasColumnName("TrainingMaxValue")
                .HasColumnType("decimal(6,2)");

            tm.Property(t => t.Unit)
                .HasColumnName("TrainingMaxUnit")
                .HasConversion<string>();
        });

        builder.Property<bool>("UseAmrap")
            .HasColumnName("UseAmrap");

        builder.Property<int>("BaseSetsPerExercise")
            .HasColumnName("BaseSetsPerExercise");

        // RepsPerSetStrategy specific properties
        builder.OwnsOne<Weight>("CurrentWeight", w =>
        {
            w.Property(wt => wt.Value)
                .HasColumnName("CurrentWeightValue")
                .HasColumnType("decimal(6,2)");

            w.Property(wt => wt.Unit)
                .HasColumnName("CurrentWeightUnit")
                .HasConversion<string>();
        });

        builder.OwnsOne<RepRange>("RepRange", rr =>
        {
            rr.Property(r => r.Minimum)
                .HasColumnName("RepRangeMinimum");

            rr.Property(r => r.Target)
                .HasColumnName("RepRangeTarget");

            rr.Property(r => r.Maximum)
                .HasColumnName("RepRangeMaximum");
        });

        builder.Property<int>("SetsPerExercise")
            .HasColumnName("SetsPerExercise");
    }
}
