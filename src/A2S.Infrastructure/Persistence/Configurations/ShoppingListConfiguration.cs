using A2S.Domain.Aggregates.Meals;
using A2S.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the ShoppingList aggregate root: one row per user per week, with the
/// menu (meal ids) and the items each stored as one jsonb value (see <see cref="JsonColumn"/>).
/// </summary>
public class ShoppingListConfiguration : IEntityTypeConfiguration<ShoppingList>
{
    public void Configure(EntityTypeBuilder<ShoppingList> builder)
    {
        builder.ToTable("ShoppingLists");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasConversion(id => id.Value, value => new ShoppingListId(value))
            .ValueGeneratedNever();

        builder.Property(l => l.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(l => l.WeekStart).IsRequired();

        builder.Ignore(l => l.SourceMealIds);
        builder.Property<List<MealId>>("_sourceMealIds")
            .HasColumnName("SourceMealIds")
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.Converter<MealId>(), JsonColumn.Comparer<MealId>())
            .HasDefaultValueSql("'[]'::jsonb")
            .IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(l => l.Items);
        builder.Property<List<ShoppingListItem>>("_items")
            .HasColumnName("Items")
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.Converter<ShoppingListItem>(), JsonColumn.Comparer<ShoppingListItem>())
            .HasDefaultValueSql("'[]'::jsonb")
            .IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(l => l.CheckedCount);

        builder.HasIndex(l => new { l.UserId, l.WeekStart }).IsUnique();

        builder.Ignore(l => l.DomainEvents);
    }
}
