namespace Sample.Components.CourierActivities
{
    using MassTransit;


    public class AllocateInventoryActivityDefinition :
        ActivityDefinition<AllocateInventoryActivity, AllocateInventoryArguments, AllocateInventoryLog>
    {
        public AllocateInventoryActivityDefinition()
        {
            ConcurrentMessageLimit = 10;
        }
    }
}