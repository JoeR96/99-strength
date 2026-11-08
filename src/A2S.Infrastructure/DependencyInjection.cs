using A2S.Application.Services;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.Repositories;
using A2S.Infrastructure.SeedData;
using A2S.Infrastructure.Security;
using A2S.Infrastructure.Services;
using A2S.Application.Interfaces;
using A2S.Application.Push;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace A2S.Infrastructure;

/// <summary>
/// Dependency injection configuration for Infrastructure layer.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds Infrastructure layer services to the DI container.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(
            configuration.GetSection(DatabaseOptions.SectionName));

        services.AddDbContext<A2SDbContext>((serviceProvider, options) =>
        {
            var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

            options.UseNpgsql(
                databaseOptions.ConnectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(A2SDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: databaseOptions.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);
                    npgsqlOptions.CommandTimeout(databaseOptions.CommandTimeout);
                });

            if (databaseOptions.EnableDetailedErrors)
            {
                options.EnableDetailedErrors();
            }

            if (databaseOptions.EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IWorkoutRepository, WorkoutRepository>();
        services.AddScoped<IExerciseDefinitionRepository, ExerciseDefinitionRepository>();

        // Whole Plan (PR 2): plan, cardio and mobility repositories plus the calendar service.
        services.AddScoped<ITrainingPlanRepository, TrainingPlanRepository>();
        services.AddScoped<ICardioProtocolRepository, CardioProtocolRepository>();
        services.AddScoped<IMobilityRoutineRepository, MobilityRoutineRepository>();
        services.AddSingleton<PlanCalendarService>();
        // Habits and body weight; the pace reading takes its checkpoints from the active TrainingPlan.
        services.AddScoped<IHabitRepository, HabitRepository>();
        services.AddScoped<IBodyWeightRepository, BodyWeightRepository>();
        services.AddSingleton<HabitStreakService>();
        services.AddSingleton<TrendService>();
        services.AddScoped<IPlanCheckpointSource, TrainingPlanCheckpointSource>();
        // Meals and the weekly shopping list; the seeder gives a user the starter recipes on first visit.
        services.AddScoped<IMealRepository, MealRepository>();
        services.AddScoped<IShoppingListRepository, ShoppingListRepository>();
        services.AddScoped<IMealSeeder, MealSeeder>();

        // Web push (PR 7b): subscriptions, the VAPID sender and the minute-by-minute nudge loop. The
        // loop is idle in Testing and when Push:* keys are absent; the sender is only built when they are.
        services.Configure<PushOptions>(configuration.GetSection(PushOptions.SectionName));
        services.AddScoped<IPushSubscriptionRepository, PushSubscriptionRepository>();
        services.AddScoped<PushNudgeService>();
        services.AddHttpClient<IPushSender, WebPushSender>();
        services.AddHostedService<PushNudgeHostedService>();

        // Hevy push/pull (Phase 6): links, snapshots and the pull cursor.
        services.AddScoped<IHevyLinkRepository, HevyLinkRepository>();
        services.AddScoped<IHevyWorkoutSnapshotRepository, HevyWorkoutSnapshotRepository>();
        services.AddScoped<IHevyPullCursorRepository, HevyPullCursorRepository>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IA2SProgramProvider, A2SProgramProvider>();

        // Every human-facing date is a Europe/London DateOnly produced through ILocalClock.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ILocalClock, LocalClock>();
        services.AddMemoryCache();
        services.AddScoped<IExerciseLibraryProvider, ExerciseLibraryProvider>();

        services.AddScoped<IDomainEventDispatcher, MediatRDomainEventDispatcher>();

        // Secrets at rest: Data Protection with the key ring persisted in Postgres so a redeploy
        // (new container, new filesystem) can still read previously protected values.
        services.AddDataProtection()
            .SetApplicationName("99-strength")
            .PersistKeysToDbContext<A2SDbContext>();
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.AddScoped<IHevyKeyResolver, UserHevyKeyResolver>();

        return services;
    }
}
