using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the HabitDefinition aggregate root.</summary>
public class HabitDefinitionConfiguration : IEntityTypeConfiguration<HabitDefinition>
{
    public void Configure(EntityTypeBuilder<HabitDefinition> builder)
    {
        builder.ToTable("Habits");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id)
            .HasConversion(id => id.Value, value => new HabitId(value))
            .ValueGeneratedNever();

        builder.Property(h => h.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(h => h.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(h => h.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(h => h.Target).HasPrecision(9, 2).IsRequired();
        builder.Property(h => h.Minimum).HasPrecision(9, 2).IsRequired();
        builder.Property(h => h.Unit).HasMaxLength(30);
        builder.Property(h => h.Order).IsRequired();
        builder.Property(h => h.Archived).IsRequired();
        builder.Property(h => h.Icon).HasMaxLength(50);
        builder.Property(h => h.ExternalKey).HasMaxLength(100);

        builder.HasIndex(h => new { h.UserId, h.Archived, h.Order });

        builder.HasIndex(h => new { h.UserId, h.ExternalKey })
            .IsUnique()
            .HasFilter("\"ExternalKey\" IS NOT NULL");

        builder.Ignore(h => h.DomainEvents);
    }
}
