using A2S.Domain.Aggregates.Workout;
using A2S.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for LinearProgressionStrategy specific properties.
/// </summary>
public class LinearProgressionStrategyConfiguration : IEntityTypeConfiguration<LinearProgressionStrategy>
{
    public void Configure(EntityTypeBuilder<LinearProgressionStrategy> builder)
    {
        // LinearProgressionStrategy specific properties
        builder.OwnsOne(lps => lps.TrainingMax, tm =>
        {
            tm.Property(t => t.Value)
                .HasColumnName("TrainingMaxValue")
                .HasColumnType("decimal(6,2)");

            tm.Property(t => t.Unit)
                .HasColumnName("TrainingMaxUnit")
                .HasConversion<string>();
        });

        builder.Property(lps => lps.UseAmrap)
            .HasColumnName("UseAmrap");

        builder.Property(lps => lps.BaseSetsPerExercise)
            .HasColumnName("BaseSetsPerExercise");
    }
}
