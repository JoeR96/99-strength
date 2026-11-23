using A2S.Domain.Aggregates.Push;
using A2S.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for FinisherSchedule: one row per user and gym session, nudges as jsonb.</summary>
public class FinisherScheduleConfiguration : IEntityTypeConfiguration<FinisherSchedule>
{
    public void Configure(EntityTypeBuilder<FinisherSchedule> builder)
    {
        builder.ToTable("FinisherSchedules");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new FinisherScheduleId(value))
            .ValueGeneratedNever();

        builder.Property(s => s.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(s => s.ClientSessionId).HasMaxLength(FinisherSchedule.MaxClientSessionIdLength).IsRequired();
        builder.Property(s => s.Day).IsRequired();
        builder.Property(s => s.Title).HasMaxLength(FinisherSchedule.MaxTextLength).IsRequired();
        builder.Property(s => s.Revision).IsRequired();
        builder.Property(s => s.UpdatedAtUtc).IsRequired();
        builder.Property(s => s.NextDueAtUtc);

        builder.Ignore(s => s.Nudges);
        builder.Property<List<FinisherNudge>>("_nudges")
            .HasColumnName("Nudges")
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.Converter<FinisherNudge>(), JsonColumn.Comparer<FinisherNudge>())
            .HasDefaultValueSql("'[]'::jsonb")
            .IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(s => s.Url);
        builder.Ignore(s => s.Tag);
        builder.Ignore(s => s.IsExhausted);
        builder.Ignore(s => s.DomainEvents);

        builder.HasIndex(s => new { s.UserId, s.ClientSessionId }).IsUnique();
        builder.HasIndex(s => s.NextDueAtUtc);
    }
}
