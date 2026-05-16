using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;

namespace Library.Integration.Tests.Sagas;

public class LibrarySagaDbContext(DbContextOptions options) : SagaDbContext(options)
{
    protected override IEnumerable<ISagaClassMap> Configurations
    {
        get
        {
            yield return new GhostSagaMap();
        }
    }
}
