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
    }


    public Event<BookCheckedOut> BookCheckedOut { get; }

    public State CheckedOut { get; }
}