using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for HevyLink: one row per (user, kind, local key).</summary>
public class HevyLinkConfiguration : IEntityTypeConfiguration<HevyLink>
{
    public void Configure(EntityTypeBuilder<HevyLink> builder)
    {
        builder.ToTable("HevyLinks");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasConversion(id => id.Value, value => new HevyLinkId(value))
            .ValueGeneratedNever();

        builder.Property(l => l.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(l => l.Kind)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(l => l.LocalKey).HasMaxLength(HevyLink.MaxLocalKeyLength).IsRequired();
        builder.Property(l => l.HevyId).HasMaxLength(HevyLink.MaxHevyIdLength).IsRequired();
        builder.Property(l => l.SyncedAtUtc).IsRequired();

        builder.HasIndex(l => new { l.UserId, l.Kind, l.LocalKey }).IsUnique();
        builder.HasIndex(l => new { l.UserId, l.Kind, l.HevyId });

        builder.Ignore(l => l.DomainEvents);
    }
}
