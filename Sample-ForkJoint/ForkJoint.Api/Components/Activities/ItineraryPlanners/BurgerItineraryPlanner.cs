namespace ForkJoint.Api.Components.Activities.ItineraryPlanners;

public interface IBurgerItineraryPlanner
{
    void PlanItinerary(Burger burger, IItineraryBuilder builder);
}

public class BurgerItineraryPlanner : IBurgerItineraryPlanner
{
    readonly Uri _dressAddress;
    readonly Uri _grillAddress;

    public BurgerItineraryPlanner(IEndpointNameFormatter formatter)
    {
        _grillAddress = new Uri($"exchange:{formatter.ExecuteActivity<GrillBurgerActivity, GrillBurgerArguments>()}");
        _dressAddress = new Uri($"exchange:{formatter.ExecuteActivity<DressBurgerActivity, DressBurgerArguments>()}");
    }

    public void PlanItinerary(Burger burger, IItineraryBuilder builder)
    {
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