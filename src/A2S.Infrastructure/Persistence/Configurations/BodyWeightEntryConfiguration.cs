using A2S.Domain.Aggregates.BodyWeight;
using A2S.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for BodyWeightEntry: one row per user per local date.</summary>
public class BodyWeightEntryConfiguration : IEntityTypeConfiguration<BodyWeightEntry>
{
    public void Configure(EntityTypeBuilder<BodyWeightEntry> builder)
    {
        builder.ToTable("BodyWeightEntries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, value => new BodyWeightEntryId(value))
            .ValueGeneratedNever();

        builder.Property(e => e.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.Date).IsRequired();
        builder.Property(e => e.Kg).HasPrecision(6, 2).IsRequired();
        builder.Property(e => e.RecordedAtUtc).IsRequired();

        builder.HasIndex(e => new { e.UserId, e.Date }).IsUnique();

        builder.Ignore(e => e.DomainEvents);
    }
}
