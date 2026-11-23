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

            // Outermost, so it sees the final answer (a validation or authorisation failure never bumps).
            config.AddOpenBehavior(typeof(ChangeVersionBehavior<,>));
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
        services.AddScoped<Hevy.HevyWorkoutPoster>();
        services.AddScoped<Hevy.HevyPostRetryRunner>();
        services.AddScoped<AmbientUser>();
        services.AddScoped<Cardio.CardioTrackSessionRecorder>();
        services.AddScoped<Cardio.CardioGateEvaluator>();
        services.AddScoped<Hevy.HevyCardioTrackLogger>();
        services.AddScoped<Hevy.SessionHevyPoster>();

        // Personal API tokens: resolves a presented token to its user for the API's token scheme.
        services.AddScoped<ApiTokens.IApiTokenAuthenticator, ApiTokens.ApiTokenAuthenticator>();

        return services;
    }
}
