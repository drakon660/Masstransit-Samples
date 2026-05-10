using Library.Contracts;
using MassTransit;

namespace Library.Components.StateMachines.ThankYou;

public class ThankYouStateMachine : MassTransitStateMachine<ThankYou>
{
    public ThankYouStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => BookReserved, x =>
        {
            x.CorrelateBy((instance, context) =>
                    context.Message.BookId == instance.BookId && context.Message.MemberId == instance.MemberId)
                .SelectId(context => context.MessageId ?? NewId.NextGuid());

            x.InsertOnInitial = true;
        });
        
        Event(() => BookCheckedOut, x =>
        {
            x.CorrelateBy((instance, context) =>
                    context.Message.BookId == instance.BookId && context.Message.MemberId == instance.MemberId)
                .SelectId(context => context.MessageId ?? NewId.NextGuid());

            x.InsertOnInitial = true;
        });

        Event(() => GetStatus, x =>
        {
            x.CorrelateBy((instance, context) => context.Message.MemberId == instance.MemberId);
            x.ReadOnly = true;

            x.OnMissingInstance(m => 
                m.ExecuteAsync(context => context.RespondAsync<ThankYouStatus>(new
                {
                    context.Message.MemberId,
                    Status = "Not Found"
                })));
        });

        Initially(When(BookReserved).Then(context =>
        {
            context.Saga.BookId = context.Message.BookId;
            context.Saga.MemberId = context.Message.MemberId;
            context.Saga.ReservationId = context.Message.ReservationId;
        }).TransitionTo(Active), When(BookCheckedOut).Then(context =>
        {
            context.Saga.BookId = context.Message.BookId;
            context.Saga.MemberId = context.Message.MemberId;
        }).TransitionTo(Active));

        During(Active,
            When(BookReserved).Then(context => { context.Saga.ReservationId = context.Message.ReservationId; }),
            Ignore(BookCheckedOut));

        CompositeEvent(() => ReadyToThank, x => x.ThankYouStatus,
            CompositeEventOptions.IncludeInitial,
            BookReserved, BookCheckedOut);

        DuringAny(
            When(GetStatus)
                .ThenAsync(async context =>
                {
                    State<ThankYou> state = await context.StateMachine.Accessor.Get(context);
                    var text = state.ToString();
                })
                .RespondAsync(context => context.Init<ThankYouStatus>(new
                {
                    context.Saga.MemberId,
                    context.Saga.BookId,
                    Status = context.StateMachine.Accessor.Get(context)
                })));

        DuringAny(When(ReadyToThank).TransitionTo(Ready));
    }

    public State Active { get; set; }
    public State Ready { get; set; }

    public Event<BookReserved> BookReserved { get; }
    public Event<BookCheckedOut> BookCheckedOut { get; }
    public Event<GetThankYouStatus> GetStatus { get; }

    public Event ReadyToThank { get; set; }
}
