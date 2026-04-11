namespace Sample.Components.CourierActivities
{
    using System;
    using System.Threading.Tasks;
    using MassTransit;
    using MassTransit.Courier;
    using Microsoft.Extensions.Logging;
    using Warehouse.Contracts;


    public class AllocateInventoryActivity(ILogger<AllocateInventoryActivity> logger, IRequestClient<AllocateInventory> client) :
        IActivity<AllocateInventoryArguments, AllocateInventoryLog>
    {
        private readonly ILogger<AllocateInventoryActivity> _logger = logger;
        private readonly IRequestClient<AllocateInventory> _client = client;

        public async Task<ExecutionResult> Execute(ExecuteContext<AllocateInventoryArguments> context)
        {
            _logger.LogInformation("[AllocateInventoryActivity]");
            var allocationId = NewId.NextGuid();
            
            try
            {
                var orderId = context.Arguments.OrderId;

                var itemNumber = context.Arguments.ItemNumber;
                if (string.IsNullOrEmpty(itemNumber))
                    throw new ArgumentNullException(nameof(itemNumber));

                var quantity = context.Arguments.Quantity;
                if (quantity <= 0.0m)
                    throw new ArgumentNullException(nameof(quantity));
                
                var response = await _client.GetResponse<InventoryAllocated>(new
                {
                    AllocationId = allocationId,
                    ItemNumber = itemNumber,
                    Quantity = quantity
                });
            }
            
            catch(Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                throw;
            }
            
            return context.Completed(new {AllocationId = allocationId});
        }

        public async Task<CompensationResult> Compensate(CompensateContext<AllocateInventoryLog> context)
        {
            await context.Publish<AllocationReleaseRequested>(new
            {
                context.Log.AllocationId,
                Reason = "Order Faulted"
            });

            return context.Compensated();
        }
    }
}