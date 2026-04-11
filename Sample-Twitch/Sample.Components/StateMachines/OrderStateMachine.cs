namespace Sample.Components.StateMachines
{
    using System;
    using System.Linq;
    using Contracts;
    using MassTransit;
    using MassTransit.Logging;
    using Microsoft.Extensions.Logging;
    using OrderStateMachineActivities;


    public class OrderStateMachine :
        MassTransitStateMachine<OrderState>
    {
        public OrderStateMachine()
        {
            Event(() => OrderSubmitted, x => x.CorrelateById(m => m.Message.OrderId));
            Event(() => OrderAccepted, x => x.CorrelateById(m => m.Message.OrderId));
            Event(() => ChangeCardNumberRequested, x => x.CorrelateById(m => m.Message.OrderId));
            Event(() => FulfillmentCompleted, x => x.CorrelateById(m => m.Message.OrderId));
            Event(() => FulfillmentFaulted, x => x.CorrelateById(m => m.Message.OrderId));
            Event(() => FulfillOrderFaulted, x => x.CorrelateById(m => m.Message.Message.OrderId));
            Event(() => OrderStatusRequested, x =>
            {
                x.CorrelateById(m => m.Message.OrderId);
                x.OnMissingInstance(m => m.ExecuteAsync(async context =>
                {
                    if (context.RequestId.HasValue)
                    {
                        await context.RespondAsync<OrderNotFound>(new { context.Message.OrderId });
                    }
                }));
            });

            //redis will not work because redisrepository doesn't support queries
            Event(() => AccountClosed, x => x.CorrelateBy((saga, context) => saga.CustomerNumber == context.Message.CustomerNumber));

            InstanceState(x => x.CurrentState);

            Initially(
                When(OrderSubmitted)
                    .Then(context =>
                    {
                        context.Saga.SubmitDate = context.Message.Timestamp;
                        context.Saga.CustomerNumber = context.Message.CustomerNumber;
                        context.Saga.PaymentCardNumber = context.Message.PaymentCardNumber;
                        context.Saga.Updated = DateTime.UtcNow;
                    })
                    .TransitionTo(Submitted));

            During(Submitted,
                Ignore(OrderSubmitted),
                When(AccountClosed)
                    .TransitionTo(Canceled),
                When(OrderAccepted)
                    .Activity(x => x.OfType<AcceptOrderActivity>())
                    .TransitionTo(Accepted));

            During(Accepted,
                When(FulfillOrderFaulted)
                    .Then(context =>
                    {
                        context.Saga.FaultReason = context.Message.Exceptions.FirstOrDefault()?.Message;
                        context.Saga.Updated = DateTime.UtcNow;
                    })
                    .TransitionTo(Faulted),
                When(FulfillmentFaulted)
                    .Then(context =>
                    {
                        context.Saga.FaultReason = "Fulfillment faulted";
                        context.Saga.Updated = DateTime.UtcNow;
                    })
                    .TransitionTo(Faulted),
                When(FulfillmentCompleted)
                    .TransitionTo(Completed));

            During(Faulted,
                When(ChangeCardNumberRequested)
                    .ThenAsync(async context =>
                    {
                        context.Saga.PaymentCardNumber = context.Message.PaymentCardNumber;
                        context.Saga.FaultReason = null;
                        context.Saga.Updated = DateTime.UtcNow;

                        var sendEndpoint = await context.GetSendEndpoint(new Uri("queue:fulfill-order"));
                        await sendEndpoint.Send<FulfillOrder>(new
                        {
                            OrderId = context.Saga.CorrelationId,
                            context.Saga.CustomerNumber,
                            context.Saga.PaymentCardNumber,
                        });
                    })
                    .TransitionTo(Accepted));

            DuringAny(
                When(OrderSubmitted)
                    .Then(context =>
                    {
                        context.Saga.SubmitDate ??= context.Message.Timestamp;
                        context.Saga.CustomerNumber ??= context.Message.CustomerNumber;
                        context.Saga.PaymentCardNumber ??= context.Message.PaymentCardNumber;
                    }),
                When(OrderStatusRequested)
                    .RespondAsync(x => x.Init<OrderStatus>(new
                    {
                        OrderId = x.Saga.CorrelationId,
                        State = x.Saga.CurrentState,
                        PaymentCardNumber = x.Saga.PaymentCardNumber
                    }))
            );
        }

        public State Submitted { get; private set; }
        public State Accepted { get; private set; }
        public State Canceled { get; private set; }
        public State Faulted { get; private set; }
        public State Completed { get; private set; }

        public Event<OrderSubmitted> OrderSubmitted { get; private set; }
        public Event<OrderAccepted> OrderAccepted { get; private set; }
        public Event<OrderFulfillmentCompleted> FulfillmentCompleted { get; private set; }
        public Event<OrderFulfillmentFaulted> FulfillmentFaulted { get; private set; }
        public Event<CheckOrder> OrderStatusRequested { get; private set; }
        public Event<CustomerAccountClosed> AccountClosed { get; private set; }
        public Event<ChangeCardNumber> ChangeCardNumberRequested { get; private set; }
        public Event<Fault<FulfillOrder>> FulfillOrderFaulted { get; private set; }
    }
}
