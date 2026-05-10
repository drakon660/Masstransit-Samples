using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;

namespace Library.Integration.Tests.Sagas;

public class LibrarySagaDbContext : SagaDbContext
{
    public LibrarySagaDbContext(DbContextOptions options) : base(options) { }

    protected override IEnumerable<ISagaClassMap> Configurations
    {
        get
        {
            yield return new GhostSagaMap();
        }
    }
}
