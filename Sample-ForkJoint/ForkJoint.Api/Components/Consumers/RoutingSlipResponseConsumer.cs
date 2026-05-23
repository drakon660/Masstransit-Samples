using MassTransit.Courier.Contracts;
using MassTransit.Events;
using MassTransit.Metadata;

namespace ForkJoint.Api.Components.Consumers;

public abstract class RoutingSlipResponseConsumer<TRequest, TResponse> :
    RoutingSlipResponseConsumer<TRequest, TResponse, Fault<TRequest>>
    where TRequest : class
    where TResponse : class
{
    protected RoutingSlipResponseConsumer(ILogger logger) : base(logger) { }

    protected override Task<Fault<TRequest>> CreateFaultedResponseMessage(ConsumeContext<RoutingSlipFaulted> context,
        TRequest request, Guid requestId)
    {
        IEnumerable<ExceptionInfo> exceptions = context.Message.ActivityExceptions.Select(x => x.ExceptionInfo);

        Fault<TRequest> response = new FaultEvent<TRequest>(request, requestId, context.Host, exceptions,
            TypeMetadataCache<TRequest>.MessageTypeNames);

        return Task.FromResult(response);
    }
}

public abstract class RoutingSlipResponseConsumer<TRequest, TResponse, TFaultResponse> :
    IConsumer<RoutingSlipCompleted>,
    IConsumer<RoutingSlipFaulted>
    where TRequest : class
    where TResponse : class
    where TFaultResponse : class
{
    protected ILogger Logger { get; }

    protected RoutingSlipResponseConsumer(ILogger logger)
    {
        Logger = logger;
    }

    public async Task Consume(ConsumeContext<RoutingSlipCompleted> context)
    {
        Logger.LogInformation("RoutingSlipCompleted: TrackingNumber={TrackingNumber} Duration={Duration} VarCount={VarCount} Keys=[{Keys}]",
            context.Message.TrackingNumber, context.Message.Duration, context.Message.Variables.Count,
            string.Join(",", context.Message.Variables.Keys));

        var requestId = GetValVar<Guid>(context, "RequestId");
        var destinationAddress = GetRefVar<Uri>(context, "ResponseAddress");
        var request = GetRefVar<TRequest>(context, "Request");

        if (requestId == Guid.Empty || destinationAddress is null || request is null)
        {
            Logger.LogWarning("Response NOT expected. requestId={RequestId} destination={Destination} requestNull={RequestNull}",
                requestId, destinationAddress, request is null);
            return;
        }

        var deadline = GetValVar<DateTime>(context, "Deadline");
        if (deadline != default && deadline <= DateTime.UtcNow)
        {
            Logger.LogWarning("Deadline passed: {Deadline}. Skipping response.", deadline);
            return;
        }

        Logger.LogInformation("Sending response to {Destination} requestId={RequestId}", destinationAddress, requestId);

        var endpoint = await context.GetResponseEndpoint<TResponse>(destinationAddress, requestId).ConfigureAwait(false);
        var response = await CreateResponseMessage(context, request);
        await endpoint.Send(response).ConfigureAwait(false);

        Logger.LogInformation("Response {ResponseType} sent.", typeof(TResponse).Name);
    }

    public async Task Consume(ConsumeContext<RoutingSlipFaulted> context)
    {
        Logger.LogInformation("RoutingSlipFaulted: TrackingNumber={TrackingNumber} Exceptions={Count} Keys=[{Keys}]",
            context.Message.TrackingNumber, context.Message.ActivityExceptions.Length,
            string.Join(",", context.Message.Variables.Keys));

        foreach (var ex in context.Message.ActivityExceptions)
            Logger.LogError("Activity {Activity} faulted: {Type}: {Message}", ex.Name, ex.ExceptionInfo.ExceptionType, ex.ExceptionInfo.Message);

        var requestId = GetValVar<Guid>(context, "RequestId");
        var destinationAddress = GetRefVar<Uri>(context, "ResponseAddress");
        var request = GetRefVar<TRequest>(context, "Request");

        if (requestId == Guid.Empty || destinationAddress is null || request is null)
        {
            Logger.LogWarning("Fault response NOT expected. requestId={RequestId} destination={Destination} requestNull={RequestNull}",
                requestId, destinationAddress, request is null);
            return;
        }

        var deadline = GetValVar<DateTime>(context, "Deadline");
        if (deadline != default && deadline <= DateTime.UtcNow)
        {
            Logger.LogWarning("Deadline passed: {Deadline}. Skipping fault response.", deadline);
            return;
        }

        var faultAddress = GetRefVar<Uri>(context, "FaultAddress");
        if (faultAddress != null)
        {
            Logger.LogInformation("Using FaultAddress {FaultAddress} instead of ResponseAddress.", faultAddress);
            destinationAddress = faultAddress;
        }

        Logger.LogInformation("Sending fault to {Destination} requestId={RequestId}", destinationAddress, requestId);

        var endpoint = await context.GetFaultEndpoint<TResponse>(destinationAddress, requestId).ConfigureAwait(false);
        var response = await CreateFaultedResponseMessage(context, request, requestId);
        await endpoint.Send(response).ConfigureAwait(false);

        Logger.LogInformation("Fault response {FaultType} sent.", typeof(TFaultResponse).Name);
    }

    protected static T? GetRefVar<T>(ConsumeContext<RoutingSlipCompleted> context, string key) where T : class
        => context.Message.Variables.ContainsKey(key) ? context.GetVariable<T>(key) : null;

    protected static T? GetRefVar<T>(ConsumeContext<RoutingSlipFaulted> context, string key) where T : class
        => context.Message.Variables.ContainsKey(key) ? context.GetVariable<T>(key) : null;

    protected static T GetValVar<T>(ConsumeContext<RoutingSlipCompleted> context, string key) where T : struct
        => context.Message.Variables.ContainsKey(key) ? context.GetVariable<T>(key) ?? default : default;

    protected static T GetValVar<T>(ConsumeContext<RoutingSlipFaulted> context, string key) where T : struct
        => context.Message.Variables.ContainsKey(key) ? context.GetVariable<T>(key) ?? default : default;

    protected abstract Task<TResponse> CreateResponseMessage(ConsumeContext<RoutingSlipCompleted> context,
        TRequest request);

    protected abstract Task<TFaultResponse> CreateFaultedResponseMessage(ConsumeContext<RoutingSlipFaulted> context,
        TRequest request, Guid requestId);
}