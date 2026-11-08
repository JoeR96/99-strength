using A2S.Domain.Aggregates.Habits;
using A2S.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for HabitLog: one row per habit per local date.</summary>
public class HabitLogConfiguration : IEntityTypeConfiguration<HabitLog>
{
    public void Configure(EntityTypeBuilder<HabitLog> builder)
    {
        builder.ToTable("HabitLogs");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasConversion(id => id.Value, value => new HabitLogId(value))
            .ValueGeneratedNever();

        builder.Property(l => l.HabitId)
            .HasConversion(id => id.Value, value => new HabitId(value))
            .IsRequired();

        builder.Property(l => l.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(l => l.Date).IsRequired();
        builder.Property(l => l.Value).HasPrecision(9, 2).IsRequired();
        builder.Property(l => l.UpdatedAtUtc).IsRequired();

        builder.HasOne<HabitDefinition>()
            .WithMany()
            .HasForeignKey(l => l.HabitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => new { l.HabitId, l.Date }).IsUnique();
        builder.HasIndex(l => new { l.UserId, l.Date });
    }
}
