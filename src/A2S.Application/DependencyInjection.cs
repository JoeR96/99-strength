using A2S.Application.Behaviors;
using A2S.Application.Common;
using A2S.Application.Services;
using A2S.Domain.Services;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace A2S.Application;

/// <summary>
/// Dependency injection configuration for Application layer.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds Application layer services to the DI container.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(assembly);

            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
            config.AddOpenBehavior(typeof(AuthorizedWorkoutBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        services.AddSingleton<VolumeBudgetService>();
        services.AddScoped<DayBudgetCalculator>();

        // Hevy push/pull (Phase 6).
        services.AddScoped<Hevy.HevyTemplateCatalogue>();
        services.AddScoped<Hevy.HevyRoutinePusher>();
        services.AddScoped<Hevy.HevySessionCompleter>();
        services.AddScoped<Hevy.HevyPullService>();
        services.AddScoped<Hevy.HevyPollRunner>();
        services.AddScoped<AmbientUser>();

        return services;
    }
}
