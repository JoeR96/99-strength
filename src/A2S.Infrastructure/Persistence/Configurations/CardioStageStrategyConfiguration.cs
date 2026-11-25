using A2S.Domain.Aggregates.Workout;
using A2S.Domain.Aggregates.Workout.Cardio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for CardioStageStrategy specific properties (TPH columns, all nullable).
/// The ladder, segments and state are records stored as jsonb.
/// </summary>
public class CardioStageStrategyConfiguration : IEntityTypeConfiguration<CardioStageStrategy>
{
    public void Configure(EntityTypeBuilder<CardioStageStrategy> builder)
    {
        builder.Property(s => s.Lane)
            .HasColumnName("CardioStage_Lane")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(s => s.Stages)
            .HasColumnName("CardioStage_Stages")
            .HasColumnType("jsonb")
            .HasConversion(
                JsonColumn.ValueConverter<IReadOnlyList<CardioStageDefinition>>(),
                JsonColumn.ValueComparer<IReadOnlyList<CardioStageDefinition>>());

        builder.Property(s => s.WarmUp)
            .HasColumnName("CardioStage_WarmUp")
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.ValueConverter<CardioSegment?>(), JsonColumn.ValueComparer<CardioSegment?>());

        builder.Property(s => s.CoolDown)
            .HasColumnName("CardioStage_CoolDown")
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.ValueConverter<CardioSegment?>(), JsonColumn.ValueComparer<CardioSegment?>());

        builder.Property(s => s.RequiresSpeed)
            .HasColumnName("CardioStage_RequiresSpeed");

        builder.Property(s => s.State)
            .HasColumnName("CardioStage_State")
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.ValueConverter<CardioStageState>(), JsonColumn.ValueComparer<CardioStageState>());
    }
}
