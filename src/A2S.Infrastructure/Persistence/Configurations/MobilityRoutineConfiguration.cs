using A2S.Domain.Aggregates.Mobility;
using A2S.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>EF Core configuration for the MobilityRoutine aggregate root; drills are owned JSON.</summary>
public class MobilityRoutineConfiguration : IEntityTypeConfiguration<MobilityRoutine>
{
    public void Configure(EntityTypeBuilder<MobilityRoutine> builder)
    {
        builder.ToTable("MobilityRoutines");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, value => new MobilityRoutineId(value))
            .ValueGeneratedNever();

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.Property(r => r.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.TargetMinutes).IsRequired();

        builder.OwnsMany(r => r.Drills, drill =>
        {
            drill.ToJson();
            drill.Property(d => d.Name).HasMaxLength(200);
            drill.Property(d => d.Sets);
            drill.Property(d => d.ExtraSetSide).HasConversion<string>();
            drill.Property(d => d.Cues).HasMaxLength(500);
            drill.Property(d => d.StandingAlternative).HasMaxLength(500);
            drill.Property(d => d.PerSide);
            drill.OwnsOne(d => d.Dose, dose =>
            {
                dose.Property(x => x.Unit).HasConversion<string>();
                dose.Property(x => x.Amount);
            });
        });
        builder.Navigation(r => r.Drills).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(r => r.DomainEvents);

        builder.HasIndex(r => r.UserId);
    }
}
