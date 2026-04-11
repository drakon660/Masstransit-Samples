namespace Sample.Components.Consumers
{
    using System.Threading.Tasks;
    using Contracts;
    using MassTransit;
    using Microsoft.Extensions.Logging;


    public class SubmitOrderConsumer(ILogger<SubmitOrderConsumer> logger) :
        IConsumer<SubmitOrder>
    {
        private readonly ILogger<SubmitOrderConsumer> _logger = logger;

        public async Task Consume(ConsumeContext<SubmitOrder> context)
        {
            _logger.LogInformation("SubmitOrderConsumer: {CustomerNumber}", context.Message.CustomerNumber);

            if (context.Message.CustomerNumber.Contains("TEST"))
            {
                if (context.RequestId != null)
                    await context.RespondAsync<OrderSubmissionRejected>(new
                    {
                        InVar.Timestamp,
                        context.Message.OrderId,
                        context.Message.CustomerNumber,
                        Reason = $"Test Customer cannot submit orders: {context.Message.CustomerNumber}"
                    });

                return;
            }

            await context.Publish<OrderSubmitted>(new
            {
                context.Message.OrderId,
                context.Message.Timestamp,
                context.Message.CustomerNumber,
                context.Message.PaymentCardNumber,
                context.Message.Notes
            });

            if (context.RequestId != null)
                await context.RespondAsync<OrderSubmissionAccepted>(new
                {
                    InVar.Timestamp,
                    context.Message.OrderId,
                    context.Message.CustomerNumber
                });
        }
    }
}