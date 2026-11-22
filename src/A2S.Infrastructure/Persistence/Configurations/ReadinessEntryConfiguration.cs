using A2S.Domain.Aggregates.Readiness;
using A2S.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for ReadinessEntry: one row per user per local date.</summary>
public class ReadinessEntryConfiguration : IEntityTypeConfiguration<ReadinessEntry>
{
    public void Configure(EntityTypeBuilder<ReadinessEntry> builder)
    {
        builder.ToTable("ReadinessEntries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasConversion(id => id.Value, value => new ReadinessEntryId(value))
            .ValueGeneratedNever();

        builder.Property(e => e.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.Date).IsRequired();
        builder.Property(e => e.Colour).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(e => e.KeepFinisher).IsRequired();
        builder.Property(e => e.UpdatedAtUtc).IsRequired();

        builder.HasIndex(e => new { e.UserId, e.Date }).IsUnique();

        builder.Ignore(e => e.FinisherSkipped);
        builder.Ignore(e => e.DomainEvents);
    }
}
