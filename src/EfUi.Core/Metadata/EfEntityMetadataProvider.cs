using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfUi.Core.Metadata;

public sealed class EfEntityMetadataProvider : IEntityMetadataProvider
{
    public IReadOnlyList<EntityMetadata> GetEntities(DbContext dbContext)
    {
        return dbContext.Model.GetEntityTypes()
            .Where(x => x.ClrType.IsClass)
            .Select(Build)
            .OrderBy(x => x.RouteName)
            .ToList();
    }

    public EntityMetadata GetEntity(DbContext dbContext, string routeName)
    {
        return GetEntities(dbContext).Single(x => x.RouteName == routeName);
    }

    private static EntityMetadata Build(IEntityType entityType)
    {
        var scalarProperties = entityType.GetProperties()
            .Select(property => new EntityPropertyMetadata(property.Name, property.ClrType, IsEditable(property)))
            .ToList();

        return new EntityMetadata(
            entityType.ClrType.Name,
            GetRouteName(entityType),
            entityType.ClrType,
            scalarProperties,
            scalarProperties.Where(x => x.IsEditable).ToList());
    }

    private static string GetRouteName(IEntityType entityType)
    {
        return (entityType.FindAnnotation("Relational:TableName")?.Value as string
                ?? entityType.ClrType.Name)
            .ToLowerInvariant();
    }

    private static bool IsEditable(IProperty property)
        => !property.IsPrimaryKey()
           && !property.IsShadowProperty()
           && property.PropertyInfo?.SetMethod is not null
           && IsSupportedScalar(property.ClrType);

    private static bool IsSupportedScalar(Type type)
    {
        var actual = Nullable.GetUnderlyingType(type) ?? type;

        return actual.IsEnum
            || actual == typeof(string)
            || actual == typeof(bool)
            || actual == typeof(byte)
            || actual == typeof(short)
            || actual == typeof(int)
            || actual == typeof(long)
            || actual == typeof(float)
            || actual == typeof(double)
            || actual == typeof(decimal)
            || actual == typeof(DateTime)
            || actual == typeof(Guid);
    }
}
