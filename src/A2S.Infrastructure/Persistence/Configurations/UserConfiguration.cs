using A2S.Domain.Common;
using A2S.Domain.Aggregates.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>
/// Entity Framework Core configuration for the User entity.
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasConversion(new ValueConverter<UserId, string>(
                id => id.Value,
                value => new UserId(value)))
            .HasColumnType("character varying")
            .HasMaxLength(256)
            .ValueGeneratedNever();

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.Property(u => u.HevyApiKey)
            .HasMaxLength(256)
            .IsRequired(false);

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Ignore(u => u.DomainEvents);
    }
}
