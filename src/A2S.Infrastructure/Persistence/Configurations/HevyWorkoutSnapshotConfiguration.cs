using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for HevyWorkoutSnapshot: keyed by Hevy's workout id, raw JSON kept as jsonb.</summary>
public class HevyWorkoutSnapshotConfiguration : IEntityTypeConfiguration<HevyWorkoutSnapshot>
{
    public void Configure(EntityTypeBuilder<HevyWorkoutSnapshot> builder)
    {
        builder.ToTable("HevyWorkoutSnapshots");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new HevyWorkoutId(value))
            .HasColumnName("HevyWorkoutId")
            .HasMaxLength(100)
            .ValueGeneratedNever();

        builder.Property(s => s.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(s => s.Title).HasMaxLength(HevyWorkoutSnapshot.MaxTitleLength).IsRequired();
        builder.Property(s => s.StartUtc).IsRequired();
        builder.Property(s => s.EndUtc).IsRequired();
        builder.Property(s => s.RoutineId).HasMaxLength(100);
        builder.Property(s => s.RawJson).HasColumnType("jsonb").IsRequired();
        builder.Property(s => s.LocalDate).IsRequired();

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.SessionCompletionId)
            .HasConversion(new ValueConverter<SessionCompletionId?, Guid?>(
                id => id.HasValue ? id.Value.Value : null,
                value => value.HasValue ? new SessionCompletionId(value.Value) : null));

        builder.Property(s => s.Reason).HasMaxLength(HevyWorkoutSnapshot.MaxReasonLength);
        builder.Property(s => s.UpdatedAtUtc).IsRequired();

        builder.Ignore(s => s.AwaitsMatching);
        builder.Ignore(s => s.DomainEvents);

        builder.HasIndex(s => new { s.UserId, s.Status });
        builder.HasIndex(s => new { s.UserId, s.LocalDate });
    }
}
