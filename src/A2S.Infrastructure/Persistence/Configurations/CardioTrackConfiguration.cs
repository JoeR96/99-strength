using A2S.Domain.Aggregates.Cardio;
using A2S.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the CardioTrack aggregate root. Stages, state, sessions, audit,
/// benchmarks and the undo snapshot are jsonb values (see <see cref="JsonColumn"/>): the aggregate
/// replaces them wholesale, and none of them is queried by SQL.
/// </summary>
public class CardioTrackConfiguration : IEntityTypeConfiguration<CardioTrack>
{
    public void Configure(EntityTypeBuilder<CardioTrack> builder)
    {
        builder.ToTable("CardioTracks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, value => new CardioTrackId(value))
            .ValueGeneratedNever();

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.Property(t => t.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Modality).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(t => t.TemplateKey).HasMaxLength(50);
        builder.Property(t => t.Notes).HasMaxLength(1000);
        builder.Property(t => t.StartedOn).IsRequired();

        builder.Property(t => t.State)
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.ValueConverter<CardioTrackState>(), JsonColumn.ValueComparer<CardioTrackState>())
            .IsRequired();

        builder.Property(t => t.Benchmark)
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.ValueConverter<TrackBenchmark?>(), JsonColumn.ValueComparer<TrackBenchmark?>());

        builder.Property(t => t.LastSnapshot)
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.ValueConverter<CardioTrackSnapshot?>(), JsonColumn.ValueComparer<CardioTrackSnapshot?>());

        MapList<TrackStage>(builder, "_stages", "Stages");
        MapList<CardioTrackSession>(builder, "_sessions", "Sessions");
        MapList<CardioTrackAuditEntry>(builder, "_audit", "Audit");
        MapList<BenchmarkResult>(builder, "_benchmarks", "Benchmarks");

        builder.Ignore(t => t.Stages);
        builder.Ignore(t => t.Sessions);
        builder.Ignore(t => t.Audit);
        builder.Ignore(t => t.Benchmarks);
        builder.Ignore(t => t.CurrentStage);
        builder.Ignore(t => t.Strategy);
        builder.Ignore(t => t.Prescription);
        builder.Ignore(t => t.SessionsToStepUp);
        builder.Ignore(t => t.CanUndo);
        builder.Ignore(t => t.IsHard);
        builder.Ignore(t => t.DomainEvents);

        builder.HasIndex(t => t.UserId);
    }

    private static void MapList<T>(EntityTypeBuilder<CardioTrack> builder, string field, string column)
    {
        builder.Property<List<T>>(field)
            .HasColumnName(column)
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.ValueConverter<List<T>>(), JsonColumn.ValueComparer<List<T>>())
            .HasDefaultValueSql("'[]'::jsonb")
            .IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
