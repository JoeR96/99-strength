using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace A2S.Api.Infrastructure;

/// <summary>
/// Removes actions (or whole controllers) marked <see cref="NonProductionOnlyAttribute"/> from the
/// application model when running in Production.
/// </summary>
public sealed class NonProductionActionsConvention : IApplicationModelConvention
{
    private readonly bool _isProduction;

    public NonProductionActionsConvention(bool isProduction)
    {
        _isProduction = isProduction;
    }

    public void Apply(ApplicationModel application)
    {
        if (!_isProduction)
        {
            return;
        }

        foreach (var controller in application.Controllers.ToList())
        {
            if (controller.Attributes.OfType<NonProductionOnlyAttribute>().Any())
            {
                application.Controllers.Remove(controller);
                continue;
            }

            foreach (var action in controller.Actions.ToList())
            {
                if (action.Attributes.OfType<NonProductionOnlyAttribute>().Any())
                {
                    controller.Actions.Remove(action);
                }
            }
        }
    }
}
