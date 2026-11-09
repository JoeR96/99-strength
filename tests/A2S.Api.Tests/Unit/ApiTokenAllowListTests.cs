using A2S.Api.Authentication;
using A2S.Api.Controllers;
using A2S.Api.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace A2S.Api.Tests.Unit;

/// <summary>
/// Pins exactly which routes a personal API token can reach. The list is built from the real MVC
/// endpoints (every controller action in the API assembly), so adding <c>[AllowApiToken]</c> to
/// something, or a new action to a controller that carries it, fails here until the list below is
/// changed on purpose.
/// </summary>
public class ApiTokenAllowListTests
{
    private static readonly string[] TokenReachable =
    [
        "GET api/v1/body-weight/trend",
        "PUT api/v1/body-weight/{date}",
        "GET api/v1/habits",
        "POST api/v1/habits",
        "GET api/v1/habits/history",
        "POST api/v1/habits/import",
        "GET api/v1/habits/today",
        "PUT api/v1/habits/{id:guid}",
        "POST api/v1/habits/{id:guid}/archive",
        "PUT api/v1/habits/{id:guid}/logs/{date}",
        "POST api/v1/habits/{id:guid}/logs/{date}/increment",
        "POST api/v1/habits/{id:guid}/unarchive",
        "GET api/v1/plans/today",
        "GET api/v1/users/me/api-tokens/whoami"
    ];

    private static IReadOnlyList<RouteEndpoint> ControllerEndpoints(bool isProduction)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services
            .AddControllers(options => options.Conventions.Add(new NonProductionActionsConvention(isProduction)))
            .AddApplicationPart(typeof(Program).Assembly);
        var app = builder.Build();
        app.MapControllers();

        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is not null)
            .ToList();
    }

    private static IEnumerable<string> Describe(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["ANY"];
        return methods.Select(method => $"{method} {endpoint.RoutePattern.RawText}");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ATokenReachesExactlyTheAllowList(bool isProduction)
    {
        var reachable = ControllerEndpoints(isProduction)
            .Where(endpoint => endpoint.Metadata.GetMetadata<AllowApiTokenAttribute>() is not null)
            .SelectMany(Describe)
            .ToList();

        reachable.Should().BeEquivalentTo(TokenReachable);
    }

    [Fact]
    public void EveryHabitAndBodyWeightAction_IsOnTheList_AndOnlyPlansTodayAndWhoAmIBesides()
    {
        var reachable = ControllerEndpoints(isProduction: true)
            .Where(endpoint => endpoint.Metadata.GetMetadata<AllowApiTokenAttribute>() is not null)
            .Select(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()!)
            .ToList();
        var all = ControllerEndpoints(isProduction: true)
            .Select(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()!)
            .ToList();

        foreach (var controller in new[] { typeof(HabitsController), typeof(BodyWeightController) })
        {
            reachable.Count(a => a.ControllerTypeInfo == controller)
                .Should().Be(all.Count(a => a.ControllerTypeInfo == controller));
        }

        reachable
            .Where(a => a.ControllerTypeInfo != typeof(HabitsController) && a.ControllerTypeInfo != typeof(BodyWeightController))
            .Select(a => $"{a.ControllerTypeInfo.Name}.{a.ActionName}")
            .Should().BeEquivalentTo(
                $"{nameof(PlansController)}.{nameof(PlansController.GetToday)}",
                $"{nameof(ApiTokensController)}.{nameof(ApiTokensController.WhoAmI)}");
    }

    [Fact]
    public void TokenManagementAndSecretsEndpoints_AreNotOnTheList()
    {
        var closed = ControllerEndpoints(isProduction: true)
            .Where(endpoint => endpoint.Metadata.GetMetadata<AllowApiTokenAttribute>() is null)
            .SelectMany(Describe)
            .ToList();

        closed.Should().Contain(
        [
            "POST api/v1/users/me/api-tokens",
            "GET api/v1/users/me/api-tokens",
            "DELETE api/v1/users/me/api-tokens/{id:guid}",
            "PUT api/v1/users/me/hevy-api-key",
            "GET api/v1/users/me/hevy-api-key",
            "GET api/v1/plans/active",
            "POST api/v1/plans"
        ]);
        closed.Count.Should().BeGreaterThan(TokenReachable.Length);
    }

    [Fact]
    public void NoControllerActionIsAnonymous()
    {
        var open = ControllerEndpoints(isProduction: false)
            .Where(endpoint =>
                endpoint.Metadata.GetMetadata<IAuthorizeData>() is null ||
                endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .SelectMany(Describe);

        open.Should().BeEmpty("a caller's identity must come from a checked Clerk session or an allow-listed token");
    }
}
