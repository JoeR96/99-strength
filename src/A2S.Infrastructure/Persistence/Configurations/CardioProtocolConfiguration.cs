using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the CardioProtocol aggregate root; intervals are owned JSON.</summary>
public class CardioProtocolConfiguration : IEntityTypeConfiguration<CardioProtocol>
{
    public void Configure(EntityTypeBuilder<CardioProtocol> builder)
    {
        builder.ToTable("CardioProtocols");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => new CardioProtocolId(value))
            .ValueGeneratedNever();

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.Property(p => p.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Kind)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(p => p.Notes)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(p => p.WeekIndex);
        builder.Property(p => p.RepeatWeek).IsRequired();

        // Intervals: one jsonb value (see JsonColumn) so the aggregate can reorder or replace the list freely.
        builder.Ignore(x => x.Intervals);
        builder.Property<List<CardioInterval>>("_intervals")
            .HasColumnName("Intervals")
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.Converter<CardioInterval>(), JsonColumn.Comparer<CardioInterval>())
            .HasDefaultValueSql("'[]'::jsonb")
            .IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(p => p.TotalMinutes);
        builder.Ignore(p => p.DefaultHighIntensityMinutes);
        builder.Ignore(p => p.DomainEvents);

        builder.HasIndex(p => p.UserId);
    }
}
