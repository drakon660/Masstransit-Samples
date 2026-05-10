using Library.Components.StateMachines.Ghost;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Integration.Tests.Sagas;

public class GhostSagaMap : SagaClassMap<Ghost>
{
    protected override void Configure(EntityTypeBuilder<Ghost> entity, ModelBuilder model)
    {
        entity.Property(x => x.CurrentState);
        entity.Property(x => x.GhostId);
    }
}
