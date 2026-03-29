using A2S.Domain.Aggregates.Workout;
using A2S.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for RepsPerSetStrategy specific properties.
/// </summary>
public class RepsPerSetStrategyConfiguration : IEntityTypeConfiguration<RepsPerSetStrategy>
{
    public void Configure(EntityTypeBuilder<RepsPerSetStrategy> builder)
    {
        // RepsPerSetStrategy specific properties
        // CurrentWeight is nullable - weight is deferred until after first session
        builder.OwnsOne(rps => rps.CurrentWeight, w =>
        {
            w.Property(wt => wt.Value)
                .HasColumnName("CurrentWeightValue")
                .HasColumnType("decimal(6,2)");

            w.Property(wt => wt.Unit)
                .HasColumnName("CurrentWeightUnit")
                .HasConversion<string>();
        });
        builder.Navigation(rps => rps.CurrentWeight).IsRequired(false);

        builder.OwnsOne(rps => rps.RepRange, rr =>
        {
            rr.Property(r => r.Minimum)
                .HasColumnName("RepRangeMinimum");

            rr.Property(r => r.Target)
                .HasColumnName("RepRangeTarget");

            rr.Property(r => r.Maximum)
                .HasColumnName("RepRangeMaximum");
        });

        builder.Property(rps => rps.CurrentSetCount)
            .HasColumnName("CurrentSetCount");

        builder.Property(rps => rps.StartingSets)
            .HasColumnName("StartingSets");

        builder.Property(rps => rps.TargetSets)
            .HasColumnName("TargetSets");

        builder.Property(rps => rps.Equipment)
            .HasColumnName("Equipment")
            .HasConversion<string>();

        builder.Property(rps => rps.IsUnilateral)
            .HasColumnName("IsUnilateral")
            .HasDefaultValue(false);

        builder.Property(rps => rps.PendingWeightConfirmation)
            .HasColumnName("PendingWeightConfirmation")
            .HasDefaultValue(false);

        builder.OwnsOne(rps => rps.SuggestedWeight, w =>
        {
            w.Property(wt => wt.Value)
                .HasColumnName("SuggestedWeightValue")
                .HasColumnType("decimal(6,2)");

            w.Property(wt => wt.Unit)
                .HasColumnName("SuggestedWeightUnit")
                .HasConversion<string>();
        });
        builder.Navigation(rps => rps.SuggestedWeight).IsRequired(false);
    }
}
