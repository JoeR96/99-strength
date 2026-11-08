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

        // Schedule: one jsonb value (see JsonColumn) so the aggregate can reorder or replace the list freely.
        builder.Ignore(x => x.Schedule);
        builder.Property<List<ScheduledSession>>("_schedule")
            .HasColumnName("Schedule")
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.Converter<ScheduledSession>(), JsonColumn.Comparer<ScheduledSession>())
            .HasDefaultValueSql("'[]'::jsonb")
            .IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Phases: one jsonb value (see JsonColumn) so the aggregate can reorder or replace the list freely.
        builder.Ignore(x => x.Phases);
        builder.Property<List<PlanPhase>>("_phases")
            .HasColumnName("Phases")
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.Converter<PlanPhase>(), JsonColumn.Comparer<PlanPhase>())
            .HasDefaultValueSql("'[]'::jsonb")
            .IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Checkpoints: one jsonb value (see JsonColumn) so the aggregate can reorder or replace the list freely.
        builder.Ignore(x => x.Checkpoints);
        builder.Property<List<Checkpoint>>("_checkpoints")
            .HasColumnName("Checkpoints")
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.Converter<Checkpoint>(), JsonColumn.Comparer<Checkpoint>())
            .HasDefaultValueSql("'[]'::jsonb")
            .IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // AllowanceUses: one jsonb value (see JsonColumn) so the aggregate can reorder or replace the list freely.
        builder.Ignore(x => x.AllowanceUses);
        builder.Property<List<AllowanceUse>>("_allowanceUses")
            .HasColumnName("AllowanceUses")
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.Converter<AllowanceUse>(), JsonColumn.Comparer<AllowanceUse>())
            .HasDefaultValueSql("'[]'::jsonb")
            .IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.Completions)
            .WithOne()
            .HasForeignKey("TrainingPlanId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Completions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(p => p.DomainEvents);

        builder.HasIndex(p => new { p.UserId, p.IsActive });
    }
}
