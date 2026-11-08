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

        builder.OwnsMany(p => p.Intervals, interval =>
        {
            interval.ToJson();
            interval.Property(i => i.Label).HasMaxLength(100);
            interval.Property(i => i.Seconds);
            interval.Property(i => i.Effort).HasConversion<string>();
        });
        builder.Navigation(p => p.Intervals).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(p => p.TotalMinutes);
        builder.Ignore(p => p.DomainEvents);

        builder.HasIndex(p => p.UserId);
    }
}
