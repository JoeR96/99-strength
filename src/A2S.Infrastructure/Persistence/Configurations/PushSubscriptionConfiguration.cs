using A2S.Domain.Aggregates.Push;
using A2S.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for PushSubscription: one row per user per browser endpoint.</summary>
public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.ToTable("PushSubscriptions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new PushSubscriptionId(value))
            .ValueGeneratedNever();

        builder.Property(s => s.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(s => s.Endpoint).HasMaxLength(PushSubscription.MaxEndpointLength).IsRequired();
        builder.Property(s => s.P256dh).HasMaxLength(PushSubscription.MaxKeyLength).IsRequired();
        builder.Property(s => s.Auth).HasMaxLength(PushSubscription.MaxKeyLength).IsRequired();
        builder.Property(s => s.UserAgent).HasMaxLength(PushSubscription.MaxUserAgentLength);
        builder.Property(s => s.SessionStartTime).IsRequired();
        builder.Property(s => s.CreatedAtUtc).IsRequired();

        builder.HasIndex(s => new { s.UserId, s.Endpoint }).IsUnique();

        builder.Ignore(s => s.DomainEvents);
    }
}
