using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the Meal aggregate root. Ingredients, steps and tags are each one
/// jsonb value (see <see cref="JsonColumn"/>) so the aggregate can replace the lists freely.
/// </summary>
public class MealConfiguration : IEntityTypeConfiguration<Meal>
{
    public void Configure(EntityTypeBuilder<Meal> builder)
    {
        builder.ToTable("Meals");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasConversion(id => id.Value, value => new MealId(value))
            .ValueGeneratedNever();

        builder.Property(m => m.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(m => m.Title)
            .IsRequired()
            .HasMaxLength(Meal.MaxTitleLength);

        builder.Property(m => m.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(m => m.Description)
            .IsRequired()
            .HasMaxLength(Meal.MaxDescriptionLength);

        builder.Property(m => m.CutFriendly).IsRequired();
        builder.Property(m => m.ExternalKey).HasMaxLength(Meal.MaxExternalKeyLength);

        builder.Ignore(m => m.Ingredients);
        builder.Property<List<Ingredient>>("_ingredients")
            .HasColumnName("Ingredients")
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.Converter<Ingredient>(), JsonColumn.Comparer<Ingredient>())
            .HasDefaultValueSql("'[]'::jsonb")
            .IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(m => m.Steps);
        builder.Property<List<string>>("_steps")
            .HasColumnName("Steps")
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.Converter<string>(), JsonColumn.Comparer<string>())
            .HasDefaultValueSql("'[]'::jsonb")
            .IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(m => m.Tags);
        builder.Property<List<string>>("_tags")
            .HasColumnName("Tags")
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.Converter<string>(), JsonColumn.Comparer<string>())
            .HasDefaultValueSql("'[]'::jsonb")
            .IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(m => new { m.UserId, m.Type, m.Title });

        builder.HasIndex(m => new { m.UserId, m.ExternalKey })
            .IsUnique()
            .HasFilter("\"ExternalKey\" IS NOT NULL");

        builder.Ignore(m => m.DomainEvents);
    }
}
