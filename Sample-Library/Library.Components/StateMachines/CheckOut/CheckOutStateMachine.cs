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
                    m.ExecuteAsync(z => z.RespondAsync<CheckOutNotFound>(new { z.Message.CheckOutId }))));


        Initially(When(BookCheckedOut)
            .Then(context =>
            {
                context.Saga.BookId = context.Message.BookId;
                context.Saga.MemberId = context.Message.MemberId;
                context.Saga.CheckOutDate = context.Message.Timestamp;
                context.Saga.DueDate = context.Message.Timestamp + settings.CheckOutDuration;
            })
            .Activity(x => x.OfInstanceType<NotifyMemberActivity>())
            .TransitionTo(CheckedOut));

        During(CheckedOut, When(RenewCheckOutRequested)
            .Then(context => { context.Saga.DueDate = DateTime.UtcNow + settings.CheckOutDuration; })
            .IfElse(context => context.Saga.DueDate > context.Saga.CheckOutDate + settings.CheckOutDurationLimit,
                exceeded => exceeded
                    .Then(context => context.Saga.DueDate = context.Saga.CheckOutDate + settings.CheckOutDurationLimit)
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
    }

    public Event<BookCheckedOut> BookCheckedOut { get; }
    public Event<RenewCheckOut> RenewCheckOutRequested { get; }

    public State CheckedOut { get; }
}