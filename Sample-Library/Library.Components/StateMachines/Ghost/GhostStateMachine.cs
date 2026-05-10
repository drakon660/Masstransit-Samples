using Library.Contracts;
using MassTransit;

namespace Library.Components.StateMachines.Ghost;

// Demonstrates the "stuck in Initial" pitfall:
// GhostPinged has InsertOnInitial = true but no Initially handler.
// First GhostPinged inserts a row that never transitions.
public class GhostStateMachine : MassTransitStateMachine<Ghost>
{
    public GhostStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => GhostStarted, x =>
        {
            x.CorrelateBy((instance, context) => instance.GhostId == context.Message.GhostId)
                .SelectId(context => context.MessageId ?? NewId.NextGuid());
            x.InsertOnInitial = true;
        });

        Event(() => GhostPinged, x =>
        {
            x.CorrelateBy((instance, context) => instance.GhostId == context.Message.GhostId)
                .SelectId(context => context.MessageId ?? NewId.NextGuid());
            x.InsertOnInitial = true; // intentional: shows the ghost pitfall
        });

        Initially(
            When(GhostStarted)
                .Then(ctx => ctx.Saga.GhostId = ctx.Message.GhostId)
                .TransitionTo(Active),
            // GhostPinged arrives before GhostStarted: row inserted via
            // InsertOnInitial, GhostId captured, but NO TransitionTo —
            // saga stays in Initial. Ghost instance.
            When(GhostPinged)
                .Then(ctx => ctx.Saga.GhostId = ctx.Message.GhostId));

        During(Active, When(GhostPinged).TransitionTo(Pinged));
    }

    public State Active { get; set; }
    public State Pinged { get; set; }

    public Event<GhostStarted> GhostStarted { get; }
    public Event<GhostPinged> GhostPinged { get; }
}
