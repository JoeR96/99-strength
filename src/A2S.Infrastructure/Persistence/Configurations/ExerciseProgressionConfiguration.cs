using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ExerciseProgression hierarchy (TPH - Table Per Hierarchy).
/// Base configuration only - derived types have their own configurations.
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
            .HasValue<RepsPerSetStrategy>("RepsPerSet")
            .HasValue<MinimalSetsStrategy>("MinimalSets");
    }
}
