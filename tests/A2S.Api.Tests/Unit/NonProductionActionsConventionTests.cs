using System.Reflection;
using A2S.Api.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace A2S.Api.Tests.Unit;

public class NonProductionActionsConventionTests
{
    private sealed class SampleController
    {
        [NonProductionOnly]
        public void Simulate() { }

        public void Real() { }
    }

    private static ApplicationModel BuildModel()
    {
        var application = new ApplicationModel();
        var controllerType = typeof(SampleController).GetTypeInfo();
        var controller = new ControllerModel(controllerType, controllerType.GetCustomAttributes(true));
        foreach (var method in new[] { nameof(SampleController.Simulate), nameof(SampleController.Real) })
        {
            var info = controllerType.GetMethod(method)!;
            controller.Actions.Add(new ActionModel(info, info.GetCustomAttributes(true)) { Controller = controller });
        }
        application.Controllers.Add(controller);
        return application;
    }

    [Fact]
    public void InProduction_RemovesMarkedActions_KeepsOthers()
    {
        var model = BuildModel();

        new NonProductionActionsConvention(isProduction: true).Apply(model);

        model.Controllers.Single().Actions.Select(a => a.ActionMethod.Name)
            .Should().BeEquivalentTo(new[] { nameof(SampleController.Real) });
    }

    [Fact]
    public void OutsideProduction_KeepsEveryAction()
    {
        var model = BuildModel();

        new NonProductionActionsConvention(isProduction: false).Apply(model);

        model.Controllers.Single().Actions.Should().HaveCount(2);
    }
}
