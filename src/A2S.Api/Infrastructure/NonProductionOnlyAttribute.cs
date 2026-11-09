namespace A2S.Api.Infrastructure;

/// <summary>
/// Marks a controller action that must not exist in Production (simulation, seeding, debugging aids).
/// <see cref="NonProductionActionsConvention"/> removes marked actions from routing when the host
/// environment is Production, so they return 404 there and need no runtime checks.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class NonProductionOnlyAttribute : Attribute
{
}
