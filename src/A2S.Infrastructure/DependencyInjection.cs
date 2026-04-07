using A2S.Application.Services;
using A2S.Domain.Common;
using A2S.Domain.Repositories;
using A2S.Domain.Services;
using A2S.Infrastructure.Persistence;
using A2S.Infrastructure.Repositories;
using A2S.Infrastructure.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IA2SProgramProvider, A2SProgramProvider>();
        services.AddMemoryCache();
        services.AddScoped<IExerciseLibraryProvider, ExerciseLibraryProvider>();

        services.AddScoped<IDomainEventDispatcher, MediatRDomainEventDispatcher>();

        return services;
    }
}
