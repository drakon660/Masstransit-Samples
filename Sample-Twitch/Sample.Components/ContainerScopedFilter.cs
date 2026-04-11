namespace Sample.Components
{
    using System.Threading.Tasks;
    using MassTransit;
    using Microsoft.Extensions.Logging;


    public class ContainerScopedFilter<T> :
        IFilter<ConsumeContext<T>> where T : class
    {
        readonly ILogger<ContainerScopedFilter<T>> _logger;

        public ContainerScopedFilter(ILogger<ContainerScopedFilter<T>> logger)
        {
            _logger = logger;
        }

        public Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
        {
            _logger.LogDebug("ContainerScopedFilter executed for {MessageType}", typeof(T).Name);

            return next.Send(context);
        }

        public void Probe(ProbeContext context)
        {
        }
    }
}