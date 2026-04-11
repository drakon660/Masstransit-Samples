namespace Sample.Components.StateMachines.OrderStateMachineActivities
{
    using System;
    using System.Threading.Tasks;
    using Contracts;
    using MassTransit;
    using Microsoft.Extensions.Logging;


    public class AcceptOrderActivity(ILogger<AcceptOrderActivity> logger) :
        IStateMachineActivity<OrderState, OrderAccepted>
    {
        private readonly ILogger<AcceptOrderActivity> _logger = logger;

        public void Probe(ProbeContext context)
        {
            context.CreateScope("accept-order");
        }

        public void Accept(StateMachineVisitor visitor)
        {
            visitor.Visit(this);
        }

        public async Task Execute(BehaviorContext<OrderState, OrderAccepted> context, IBehavior<OrderState, OrderAccepted> next)
        {
            _logger.LogInformation("Accepting order {OrderId}", context.Message.OrderId);
            
            var sendEndpoint = await context.GetSendEndpoint(new Uri("queue:fulfill-order"));
            
            await sendEndpoint.Send<FulfillOrder>(new
            {
                context.Message.OrderId,
                context.Saga.CustomerNumber,
                context.Saga.PaymentCardNumber,
            });

            await next.Execute(context).ConfigureAwait(false);
        }

        public Task Faulted<TException>(BehaviorExceptionContext<OrderState, OrderAccepted, TException> context, IBehavior<OrderState, OrderAccepted> next)
            where TException : Exception
        {
            return next.Faulted(context);
        }
    }
}