using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for HevyPullCursor: one row per user (the aggregate id is the user id).</summary>
public class HevyPullCursorConfiguration : IEntityTypeConfiguration<HevyPullCursor>
{
    public void Configure(EntityTypeBuilder<HevyPullCursor> builder)
    {
        builder.ToTable("HevyPullCursors");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, value => new UserId(value))
            .HasColumnName("UserId")
            .HasMaxLength(256)
            .ValueGeneratedNever();

        builder.Ignore(c => c.UserId);

        builder.Property(c => c.LastEventAtUtc).IsRequired();
        builder.Property(c => c.LastPulledAtUtc).IsRequired();

        builder.Ignore(c => c.DomainEvents);
    }
}
