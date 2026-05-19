using Microsoft.EntityFrameworkCore;

namespace EfUi.Core.Metadata;

public interface IEntityMetadataProvider
{
    EntityDiscoveryResult GetDiscoveryResult(DbContext dbContext);
    IReadOnlyList<EntityMetadata> GetEntities(DbContext dbContext);
    EntityMetadata GetEntity(DbContext dbContext, string routeName);
}
