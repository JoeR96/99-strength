using A2S.Domain.Common;
using A2S.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ExerciseDefinition reference data entity.
/// </summary>
public class ExerciseDefinitionConfiguration : IEntityTypeConfiguration<ExerciseDefinition>
{
    public void Configure(EntityTypeBuilder<ExerciseDefinition> builder)
    {
        builder.ToTable("ExerciseDefinitions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasConversion(
                id => id.Value,
                value => new ExerciseDefinitionId(value))
            .ValueGeneratedNever();

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.EquipmentType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.MuscleGroup)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.IsCompound)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.Description)
            .HasMaxLength(500);

        builder.Property(e => e.DefaultRepRangeMin);
        builder.Property(e => e.DefaultRepRangeMax);
        builder.Property(e => e.DefaultSets);

        builder.HasIndex(e => e.Name).IsUnique();
        builder.HasIndex(e => e.EquipmentType);
        builder.HasIndex(e => e.MuscleGroup);
    }
}
