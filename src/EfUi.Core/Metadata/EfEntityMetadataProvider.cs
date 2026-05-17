using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfUi.Core.Metadata;

public sealed class EfEntityMetadataProvider : IEntityMetadataProvider
{
    public IReadOnlyList<EntityMetadata> GetEntities(DbContext dbContext)
    {
        var routeNames = GetRouteNames(dbContext);

        return dbContext.Model.GetEntityTypes()
            .Where(x => x.ClrType.IsClass)
            .Select(entityType => Build(entityType, routeNames))
            .OrderBy(x => x.RouteName)
            .ToList();
    }

    public EntityMetadata GetEntity(DbContext dbContext, string routeName)
    {
        return GetEntities(dbContext).Single(x => x.RouteName == routeName);
    }

    private static EntityMetadata Build(IEntityType entityType, IReadOnlyDictionary<Type, string> routeNames)
    {
        var scalarProperties = entityType.GetProperties()
            .Select(property => new EntityPropertyMetadata(property.Name, property.ClrType, IsEditable(property)))
            .ToList();

        return new EntityMetadata(
            entityType.ClrType.Name,
            routeNames.TryGetValue(entityType.ClrType, out var routeName)
                ? routeName
                : entityType.ClrType.Name.ToLowerInvariant(),
            entityType.ClrType,
            scalarProperties,
            scalarProperties.Where(x => x.IsEditable).ToList());
    }

    private static IReadOnlyDictionary<Type, string> GetRouteNames(DbContext dbContext)
    {
        return dbContext.GetType()
            .GetProperties()
            .Where(property => property.PropertyType.IsGenericType
                && property.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .ToDictionary(
                property => property.PropertyType.GetGenericArguments()[0],
                property => property.Name.ToLowerInvariant());
    }

    private static bool IsEditable(IProperty property)
        => !property.IsPrimaryKey()
           && !property.IsShadowProperty()
           && property.PropertyInfo?.SetMethod is not null;
}
