namespace Sample.Components.StateMachines
{
    using System;
    using MassTransit;
    using MongoDB.Bson;
    using MongoDB.Bson.Serialization.Attributes;

    //using MongoDB.Bson.Serialization.Attributes;


    public class OrderState :
        SagaStateMachineInstance,
       ISagaVersion
    {
        public string CurrentState { get; set; }

        public string CustomerNumber { get; set; }
        public string PaymentCardNumber { get; set; }

        public string FaultReason { get; set; }

        public DateTime? SubmitDate { get; set; }
        public DateTime? Updated { get; set; }

        public int Version { get; set; }

        [BsonId]
        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid CorrelationId { get; set; }
    }
}