using A2S.Domain.Aggregates.HevySync;
using A2S.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for HevyWorkoutPost: one row per (user, client session id).</summary>
public class HevyWorkoutPostConfiguration : IEntityTypeConfiguration<HevyWorkoutPost>
{
    public void Configure(EntityTypeBuilder<HevyWorkoutPost> builder)
    {
        builder.ToTable("HevyWorkoutPosts");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => new HevyWorkoutPostId(value))
            .ValueGeneratedNever();

        builder.Property(p => p.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(p => p.ClientSessionId).IsRequired();

        builder.Property(p => p.PlanId)
            .HasConversion(new ValueConverter<TrainingPlanId?, Guid?>(
                id => id.HasValue ? id.Value.Value : null,
                value => value.HasValue ? new TrainingPlanId(value.Value) : null));

        builder.Property(p => p.CompletionId)
            .HasConversion(new ValueConverter<SessionCompletionId?, Guid?>(
                id => id.HasValue ? id.Value.Value : null,
                value => value.HasValue ? new SessionCompletionId(value.Value) : null));

        builder.Property(p => p.LocalDate).IsRequired();
        builder.Property(p => p.Title).HasMaxLength(HevyWorkoutPost.MaxTitleLength).IsRequired();
        builder.Property(p => p.StartedAtUtc).IsRequired();
        builder.Property(p => p.EndedAtUtc).IsRequired();
        builder.Property(p => p.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(p => p.ResultJson).HasColumnType("jsonb").IsRequired();
        builder.Property(p => p.CreatedAtUtc).IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.Attempts).IsRequired();
        builder.Property(p => p.NextAttemptAtUtc).IsRequired();
        builder.Property(p => p.LastError).HasMaxLength(HevyWorkoutPost.MaxErrorLength);
        builder.Property(p => p.HevyWorkoutId).HasMaxLength(100);

        builder.Ignore(p => p.MayAlreadyExistInHevy);
        builder.Ignore(p => p.DomainEvents);

        builder.HasIndex(p => new { p.UserId, p.ClientSessionId }).IsUnique();
        builder.HasIndex(p => new { p.Status, p.NextAttemptAtUtc });
        builder.HasIndex(p => new { p.UserId, p.LocalDate });
    }
}
