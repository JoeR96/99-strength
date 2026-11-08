using A2S.Domain.Aggregates.Plan;
using A2S.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace A2S.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the TrainingPlan aggregate root. Schedule, phases, checkpoints and
/// allowance uses are owned JSON collections; completions live in the SessionCompletions child table.
/// </summary>
public class TrainingPlanConfiguration : IEntityTypeConfiguration<TrainingPlan>
{
    public void Configure(EntityTypeBuilder<TrainingPlan> builder)
    {
        builder.ToTable("TrainingPlans");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => new TrainingPlanId(value))
            .ValueGeneratedNever();

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.Property(p => p.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.StartDate).IsRequired();
        builder.Property(p => p.IsActive).IsRequired();

        builder.Property(p => p.LinkedWorkoutId)
            .HasConversion(new ValueConverter<WorkoutId?, Guid?>(
                id => id.HasValue ? id.Value.Value : null,
                value => value.HasValue ? new WorkoutId(value.Value) : null));

        builder.Property(p => p.StartWeightKg)
            .HasPrecision(6, 2)
            .IsRequired();

        builder.OwnsOne(p => p.Allowances, allowances =>
        {
            allowances.Property(a => a.FlexibleMeals).HasColumnName("FlexibleMealsPerWeek").IsRequired();
            allowances.Property(a => a.AlcoholOccasions).HasColumnName("AlcoholOccasionsPerWeek").IsRequired();
        });
        builder.Navigation(p => p.Allowances).IsRequired();

        builder.OwnsMany(p => p.Schedule, session =>
        {
            session.ToJson();
            session.Property(s => s.Day).HasConversion<string>();
            session.Property(s => s.Kind).HasConversion<string>();
            session.Property(s => s.LiftDay).HasConversion<string>();
            session.Property(s => s.CardioProtocolId)
                .HasConversion(new ValueConverter<CardioProtocolId?, Guid?>(
                    id => id.HasValue ? id.Value.Value : null,
                    value => value.HasValue ? new CardioProtocolId(value.Value) : null));
            session.Property(s => s.MobilityRoutineId)
                .HasConversion(new ValueConverter<MobilityRoutineId?, Guid?>(
                    id => id.HasValue ? id.Value.Value : null,
                    value => value.HasValue ? new MobilityRoutineId(value.Value) : null));
            session.Property(s => s.Label).HasMaxLength(100);
            session.Ignore(s => s.DayOrder);
        });
        builder.Navigation(p => p.Schedule).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(p => p.Phases, phase =>
        {
            phase.ToJson();
            phase.Property(x => x.Type).HasConversion<string>();
            phase.Property(x => x.From);
            phase.Property(x => x.To);
            phase.Property(x => x.Note).HasMaxLength(500);
        });
        builder.Navigation(p => p.Phases).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(p => p.Checkpoints, checkpoint =>
        {
            checkpoint.ToJson();
            checkpoint.Property(x => x.Date);
            checkpoint.Property(x => x.TargetKg);
            checkpoint.Property(x => x.Note).HasMaxLength(500);
        });
        builder.Navigation(p => p.Checkpoints).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(p => p.AllowanceUses, use =>
        {
            use.ToJson();
            use.Property(x => x.Date);
            use.Property(x => x.Kind).HasConversion<string>();
        });
        builder.Navigation(p => p.AllowanceUses).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.Completions)
            .WithOne()
            .HasForeignKey("TrainingPlanId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Completions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(p => p.DomainEvents);

        builder.HasIndex(p => new { p.UserId, p.IsActive });
    }
}
