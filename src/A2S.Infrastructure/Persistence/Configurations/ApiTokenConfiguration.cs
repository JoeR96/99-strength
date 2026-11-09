using A2S.Domain.Aggregates.ApiTokens;
using A2S.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ApiToken: one row per personal API token. The token is looked up by its
/// hash on every token-authenticated request, so the hash carries a unique index.
/// </summary>
public class ApiTokenConfiguration : IEntityTypeConfiguration<ApiToken>
{
    public void Configure(EntityTypeBuilder<ApiToken> builder)
    {
        builder.ToTable("ApiTokens");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, value => new ApiTokenId(value))
            .ValueGeneratedNever();

        builder.Property(t => t.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(t => t.Name).HasMaxLength(ApiToken.MaxNameLength).IsRequired();
        builder.Property(t => t.TokenHash).HasMaxLength(ApiToken.HashLength).IsRequired();
        builder.Property(t => t.Prefix).HasMaxLength(ApiToken.DisplayPrefixLength).IsRequired();
        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.LastUsedAtUtc);
        builder.Property(t => t.RevokedAtUtc);

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.UserId);

        builder.Ignore(t => t.IsActive);
        builder.Ignore(t => t.DomainEvents);
    }
}
