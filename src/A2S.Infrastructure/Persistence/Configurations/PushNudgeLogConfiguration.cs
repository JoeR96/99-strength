using A2S.Domain.Aggregates.Push;
using A2S.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for PushNudgeLog: one row per user, local date and nudge kind.</summary>
public class PushNudgeLogConfiguration : IEntityTypeConfiguration<PushNudgeLog>
{
    public void Configure(EntityTypeBuilder<PushNudgeLog> builder)
    {
        builder.ToTable("PushNudgeLogs");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasConversion(id => id.Value, value => new PushNudgeLogId(value))
            .ValueGeneratedNever();

        builder.Property(l => l.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(l => l.LocalDate).IsRequired();
        builder.Property(l => l.Kind).HasConversion<int>().IsRequired();
        builder.Property(l => l.SentAtUtc).IsRequired();

        builder.HasIndex(l => new { l.UserId, l.LocalDate, l.Kind }).IsUnique();

        builder.Ignore(l => l.DomainEvents);
    }
}
