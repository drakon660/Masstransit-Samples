using Library.Components.Consumers;
using Library.Contracts;
using MassTransit;

namespace Library.Components.StateMachines.BookReturnStateMachine;

public class BookReturnStateMachine : MassTransitStateMachine<BookReturn>
{
    public BookReturnStateMachine(IEndpointNameFormatter formatter)
    {
        Event(() => BookReturned, x => x.CorrelateById(m => m.Message.CheckOutId));

        InstanceState(x => x.CurrentState);
        Request(() => ChargeFine, x =>
        {
            var endpoint = formatter.Consumer<ChargeFineConsumer>();

            x.ServiceAddress = new Uri($"queue:{endpoint}");
            x.Timeout = TimeSpan.FromSeconds(10);
        });

        Initially(
            When(BookReturned)
                .Then(context =>
                {
                    context.Saga.BookId = context.Message.BookId;
                    context.Saga.MemberId = context.Message.MemberId;
                    context.Saga.CheckOutDate = context.Message.Timestamp;
                    context.Saga.DueDate = context.Message.DueDate;
                    context.Saga.ReturnDate = context.Message.ReturnDate;
                }).IfElse(context => context.Saga.ReturnDate > context.Saga.DueDate, late =>
                        late.Request(ChargeFine, context => context.Init<ChargeMemberFine>(new
                        {
                            context.Saga.MemberId,
                            Amount = 123.45m
                        })).TransitionTo(ChargingFine),
                    onTime => onTime.TransitionTo(Complete)));

        During(ChargingFine, 
            When(ChargeFine.Completed).TransitionTo(Complete),
            When(ChargeFine.Faulted).TransitionTo(FailedToFineMember),
            When(ChargeFine.TimeoutExpired).TransitionTo(FailedToFineMember)
        );
    }

    public State ChargingFine { get; } = null!;
    public State FailedToFineMember { get; } = null!;
    public State Complete { get; } = null!;
    public Event<BookReturned> BookReturned { get; } = null!;

    public Request<BookReturn, ChargeMemberFine, FineCharged> ChargeFine { get; } = null!;
}
