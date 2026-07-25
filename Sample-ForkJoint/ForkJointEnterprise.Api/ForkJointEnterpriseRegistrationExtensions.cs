using ForkJointEnterprise.Api.Components.Activities.DressBurger;
using ForkJointEnterprise.Api.Components.Activities.GrillBurger;
using ForkJointEnterprise.Api.Components.Activities.ItineraryPlanners;
using ForkJointEnterprise.Api.Components.Consumers;
using ForkJointEnterprise.Api.Components.StateMachines;
using ForkJointEnterprise.Api.Services;
using ForkJointEnterprise.Contracts;
using MassTransit;
using MassTransit.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ForkJointEnterprise.Api;

public static class ForkJointEnterpriseRegistrationExtensions
{
    public static IBusRegistrationConfigurator AddForkJointEnterpriseComponents(this IBusRegistrationConfigurator configurator)
    {
        configurator.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter("fork-joint-enterprise", false));

        // Activities
        configurator.AddActivity<GrillBurgerActivity, GrillBurgerArguments, GrillBurgerLog>();
        configurator.AddExecuteActivity<DressBurgerActivity, DressBurgerArguments>();

        // Consumers
        configurator.AddConsumer<CookFryConsumer>();
        configurator.AddConsumer<PourShakeConsumer>();
        configurator.AddConsumer<CookOnionRingsConsumer>();

        // Sagas
        configurator.AddSagaStateMachine<BurgerStateMachine, BurgerState, BurgerSagaDefinition>()
            .InMemoryRepository();

        configurator.AddSagaStateMachine<FryStateMachine, FryState, FrySagaDefinition>()
            .InMemoryRepository();

        configurator.AddSagaStateMachine<ShakeStateMachine, ShakeState, ShakeSagaDefinition>()
            .InMemoryRepository();

        configurator.AddSagaStateMachine<FryShakeStateMachine, FryShakeState, FryShakeSagaDefinition>()
            .InMemoryRepository();

        configurator.AddSagaStateMachine<OnionRingsStateMachine, OnionRingsState, OnionRingsSagaDefinition>()
            .InMemoryRepository();

        configurator.AddSagaStateMachine<OrderStateMachine, OrderState, OrderSagaDefinition>()
            .InMemoryRepository();

        configurator.AddSagaStateMachine<RequestStateMachine, RequestState, RequestSagaDefinition>()
            .InMemoryRepository();

        // Request clients
        configurator.AddRequestClient<SubmitOrder>();
        configurator.AddRequestClient<OrderOnionRings>();

        return configurator;
    }

    public static IServiceCollection AddForkJointEnterpriseServices(this IServiceCollection services)
    {
        services.TryAddScoped<IBurgerItineraryPlanner, BurgerItineraryPlanner>();
        services.TryAddSingleton<IGrill, Grill>();
        services.TryAddSingleton<IFryer, Fryer>();
        services.TryAddSingleton<IShakeMachine, ShakeMachine>();

        return services;
    }
}
