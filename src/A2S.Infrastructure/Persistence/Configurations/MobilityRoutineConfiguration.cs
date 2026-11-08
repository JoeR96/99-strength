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

        // Drills: one jsonb value (see JsonColumn) so the aggregate can reorder or replace the list freely.
        builder.Ignore(x => x.Drills);
        builder.Property<List<MobilityDrill>>("_drills")
            .HasColumnName("Drills")
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.Converter<MobilityDrill>(), JsonColumn.Comparer<MobilityDrill>())
            .HasDefaultValueSql("'[]'::jsonb")
            .IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(r => r.DomainEvents);

        builder.HasIndex(r => r.UserId);
    }
}
