using A2S.Domain.Aggregates.Workout;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for MinimalSetsStrategy specific properties.
/// </summary>
public class MinimalSetsStrategyConfiguration : IEntityTypeConfiguration<MinimalSetsStrategy>
{
    public void Configure(EntityTypeBuilder<MinimalSetsStrategy> builder)
    {
        // MinimalSetsStrategy specific properties
        builder.OwnsOne(mss => mss.CurrentWeight, w =>
        {
            w.Property(wt => wt.Value)
                .HasColumnName("MinimalSets_CurrentWeightValue")
                .HasColumnType("decimal(6,2)");

            w.Property(wt => wt.Unit)
                .HasColumnName("MinimalSets_CurrentWeightUnit")
                .HasConversion<string>();
        });

        builder.Property(mss => mss.TargetTotalReps)
            .HasColumnName("MinimalSets_TargetTotalReps");

        builder.Property(mss => mss.CurrentSetCount)
            .HasColumnName("MinimalSets_CurrentSetCount");

        builder.Property(mss => mss.StartingSets)
            .HasColumnName("MinimalSets_StartingSets");

        builder.Property(mss => mss.MinimumSets)
            .HasColumnName("MinimalSets_MinimumSets");

        builder.Property(mss => mss.MaximumSets)
            .HasColumnName("MinimalSets_MaximumSets");

        builder.Property(mss => mss.Equipment)
            .HasColumnName("MinimalSets_Equipment")
            .HasConversion<string>();
    }
}
