namespace Warehouse.Components.StateMachines
{
    using System;
    using MassTransit;
    using MongoDB.Bson;
    using MongoDB.Bson.Serialization.Attributes;


    public class AllocationState :
        SagaStateMachineInstance,
        ISagaVersion
    {
        public string CurrentState { get; set; }
        
        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid? HoldDurationToken { get; set; }

        public int Version { get; set; }

        [BsonId]
        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid CorrelationId { get; set; }
    }
}