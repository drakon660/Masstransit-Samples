using Library.Contracts;
using MassTransit;

namespace Library.Components.StateMachines.CheckOut;

public class CheckOutStateMachine : MassTransitStateMachine<CheckOut>
{
    private readonly CheckOutSettings _settings;

    public CheckOutStateMachine(CheckOutSettings settings)
    {
        _settings = settings;

        InstanceState(x => x.CurrentState);

        Event(() => BookCheckedOut, x =>
            x.CorrelateById(m => m.Message.CheckOutId));
        
        Event(() => AddedToCollection, x =>
            x.CorrelateBy((instance, context) =>
                instance.BookId == context.Message.BookId && instance.MemberId == context.Message.MemberId));
        
        Event(() => AddedToCollectionFaulted, x =>
            x.CorrelateBy((instance, context) =>
                instance.BookId == context.Message.Message.BookId && instance.MemberId == context.Message.Message.MemberId));
        
        Event(() => RenewCheckOutRequested, x =>
            x.CorrelateById(m => m.Message.CheckOutId)
                .OnMissingInstance(m =>
                    m.ExecuteAsync(z => z.RespondAsync<CheckOutNotFound>(new { z.Message.CheckOutId }))));


        Initially(When(BookCheckedOut)
            .Then(context =>
            {
                context.Saga.BookId = context.Message.BookId;
                context.Saga.MemberId = context.Message.MemberId;
                context.Saga.CheckOutDate = context.Message.Timestamp;
                context.Saga.DueDate = context.Message.Timestamp + _settings.CheckOutDuration;
            })
            .Activity(x => x.OfInstanceType<NotifyMemberActivity>())
            .PublishAsync(x=>x.Init<AddBookToMemberCollection>(new
            {
                x.Saga.BookId,
                x.Saga.MemberId,
            }))
            .TransitionTo(CheckedOut));

        During(CheckedOut, When(RenewCheckOutRequested)
            .Then(context => { context.Saga.DueDate = DateTime.UtcNow + _settings.CheckOutDuration; })
            .IfElse(context => context.Saga.DueDate > context.Saga.CheckOutDate + _settings.CheckOutDurationLimit,
                exceeded => exceeded
                    .Then(context => context.Saga.DueDate = context.Saga.CheckOutDate + _settings.CheckOutDurationLimit)
                    .RespondAsync(context => context.Init<CheckOutDurationLimitReached>(new
                    {
                        context.Message.CheckOutId,
                        context.Saga.DueDate
                    })),
                otherwise => otherwise
                    .Activity(x => x.OfInstanceType<NotifyMemberActivity>())
                    .RespondAsync(context => context.Init<CheckOutRenewed>(new
                    {
                        context.Message.CheckOutId,
                        context.Saga.DueDate
                    }))));

        DuringAny(When(AddedToCollection).Then(x=>{}));
        DuringAny(When(AddedToCollectionFaulted).Then(x =>
        {
            Console.WriteLine("Add to collection faulted");
        }));
    }

    public Event<BookCheckedOut> BookCheckedOut { get; }
    public Event<RenewCheckOut> RenewCheckOutRequested { get; }
    
    public Event<BookAddedToMemberCollection> AddedToCollection { get; }
    public Event<Fault<AddBookToMemberCollection>> AddedToCollectionFaulted { get; }

    public State CheckedOut { get; }
}
