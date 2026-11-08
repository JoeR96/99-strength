using System.Text.Json;
using A2S.Domain.Common;
using A2S.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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

        // Secondary muscle groups are a jsonb string array. The column itself is added by the
        // integrator's AddSecondaryMuscleGroups migration; the default keeps existing rows valid.
        var secondaryGroupsConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => string.IsNullOrEmpty(v)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        var secondaryGroupsComparer = new ValueComparer<List<string>>(
            (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
            v => v.Aggregate(0, (hash, group) => HashCode.Combine(hash, group.GetHashCode())),
            v => v.ToList());

        builder.Ignore(e => e.SecondaryMuscleGroups);
        builder.Property<List<string>>("_secondaryMuscleGroups")
            .HasColumnName("SecondaryMuscleGroups")
            .HasColumnType("jsonb")
            .HasConversion(secondaryGroupsConverter, secondaryGroupsComparer)
            .HasDefaultValueSql("'[]'::jsonb")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(e => e.Name).IsUnique();
        builder.HasIndex(e => e.EquipmentType);
        builder.HasIndex(e => e.MuscleGroup);
    }
}
