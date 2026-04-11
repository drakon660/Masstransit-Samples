namespace Warehouse.Components.Consumers
{
    using System;
    using System.Threading.Tasks;
    using Contracts;
    using MassTransit;
    using Microsoft.Extensions.Logging;


    public class AllocateInventoryConsumer(ILogger<AllocateInventoryConsumer> logger) :
        IConsumer<AllocateInventory>
    {
        private readonly ILogger<AllocateInventoryConsumer> _logger = logger;

        public async Task Consume(ConsumeContext<AllocateInventory> context)
        {
            _logger.LogInformation("[PaymentActivity] Allocating");
            
            await context.Publish<AllocationCreated>(new
            {
                context.Message.AllocationId,
                HoldDuration = TimeSpan.FromSeconds(15),
            });

            await context.RespondAsync<InventoryAllocated>(new
            {
                context.Message.AllocationId,
                context.Message.ItemNumber,
                context.Message.Quantity
            });
        }
    }
}