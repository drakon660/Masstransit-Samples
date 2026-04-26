using Library.Contracts;
using MassTransit;

namespace Library.Components.StateMachines.CheckOut;

public class CheckOutStateMachine : MassTransitStateMachine<CheckOut>
{
    public CheckOutStateMachine(CheckOutSettings settings)
    {
        InstanceState(x => x.CurrentState);

        Event(() => BookCheckedOut, x =>
            x.CorrelateById(m => m.Message.CheckOutId));

        Event(() => RenewCheckOutRequested, x =>
            x.CorrelateById(m => m.Message.CheckOutId)
                .OnMissingInstance(m => 
                    m.ExecuteAsync(z=>z.RespondAsync<CheckOutNotFound>(z.Message))));


        Initially(When(BookCheckedOut)
            .Then(context =>
            {
                context.Saga.BookId = context.Message.BookId;
                context.Saga.MemberId = context.Message.MemberId;
                context.Saga.CheckoutDate = context.Message.Timestamp;
                context.Saga.DueDate = context.Message.Timestamp.Add(settings.CheckOutDuration);
            })
            .Activity(x => x.OfInstanceType<NotifyMemberActivity>())
            .TransitionTo(CheckedOut));

        During(CheckedOut, When(RenewCheckOutRequested)
            .Then(context => { context.Saga.DueDate = context.Saga.CheckoutDate + settings.CheckOutDuration; })
            .Activity(x => x.OfInstanceType<NotifyMemberActivity>()).RespondAsync(context =>
                context.Init<CheckOutRenewed>(new
                {
                    context.Message.CheckOutId,
                    context.Saga.DueDate,
                })));
    }


    public Event<BookCheckedOut> BookCheckedOut { get; }
    public Event<RenewCheckOut> RenewCheckOutRequested { get; }

    public State CheckedOut { get; }
}