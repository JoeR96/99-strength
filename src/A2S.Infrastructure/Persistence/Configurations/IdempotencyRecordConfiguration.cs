using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for IdempotencyRecord: one row per user and Idempotency-Key.</summary>
public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).UseIdentityByDefaultColumn();
        builder.Property(r => r.UserId).HasMaxLength(256).IsRequired();
        builder.Property(r => r.Key).HasMaxLength(128).IsRequired();
        builder.Property(r => r.Route).HasMaxLength(512).IsRequired();
        builder.Property(r => r.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(r => r.StatusCode).IsRequired();
        builder.Property(r => r.ResponseBody);
        builder.Property(r => r.CreatedAtUtc).IsRequired();

        builder.HasIndex(r => new { r.UserId, r.Key }).IsUnique();
        builder.HasIndex(r => r.CreatedAtUtc);
    }
}
