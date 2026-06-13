using ForkJointEnterprise.Api.Components.Activities.DressBurger;
using ForkJointEnterprise.Api.Components.Activities.GrillBurger;

namespace ForkJointEnterprise.Api.Components.Activities.ItineraryPlanners;

public interface IBurgerItineraryPlanner
{
    void PlanItinerary(Burger burger, IItineraryBuilder builder);
}

public class BurgerItineraryPlanner : IBurgerItineraryPlanner
{
    readonly Uri _dressAddress;
    readonly Uri _grillAddress;
    readonly ILogger<BurgerItineraryPlanner> _logger;

    public BurgerItineraryPlanner(IEndpointNameFormatter formatter, ILogger<BurgerItineraryPlanner> logger)
    {
        _grillAddress = new Uri($"exchange:{formatter.ExecuteActivity<GrillBurgerActivity, GrillBurgerArguments>()}");
        _dressAddress = new Uri($"exchange:{formatter.ExecuteActivity<DressBurgerActivity, DressBurgerArguments>()}");
        _logger = logger;
    }

    public void PlanItinerary(Burger burger, IItineraryBuilder builder)
    {
        _logger.LogInformation("BurgerItineraryPlanner: BurgerId={BurgerId} grill={Grill} dress={Dress}",
            burger.BurgerId, _grillAddress, _dressAddress);

        builder.AddActivity(nameof(GrillBurgerActivity), _grillAddress, new
        {
            burger.Weight,
            burger.Cheese,
        });

        builder.AddActivity(nameof(DressBurgerActivity), _dressAddress, new
        {
            burger.Lettuce,
            burger.Pickle,
            burger.Onion,
            burger.Ketchup,
            burger.Mustard
        });
    }
}