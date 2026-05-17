using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfUi.Core.Metadata;

public sealed class EfEntityMetadataProvider : IEntityMetadataProvider
{
    public IReadOnlyList<EntityMetadata> GetEntities(DbContext dbContext)
    {
        return dbContext.Model.GetEntityTypes()
            .Where(entityType => entityType.ClrType.IsClass)
            .Where(entityType => !IsSharedJoinEntity(entityType))
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
        var keyProperties = entityType.FindPrimaryKey()?.Properties;
        if (keyProperties is null || keyProperties.Count != 1)
        {
            throw new InvalidOperationException($"Entity '{entityType.ClrType.Name}' must have a single primary key.");
        }

        var keyProperty = keyProperties[0];

        var scalarProperties = entityType.GetProperties()
            .Select(property => new EntityPropertyMetadata(
                property.Name,
                property.ClrType,
                IsEditableOnCreate(property),
                IsEditableOnUpdate(property),
                property.Name == keyProperty.Name))
            .ToList();

        var primaryKeyMetadata = scalarProperties.Single(property => property.IsPrimaryKey);

        return new EntityMetadata(
            entityType.ClrType.Name,
            GetRouteName(entityType),
            entityType.ClrType,
            primaryKeyMetadata,
            scalarProperties,
            scalarProperties.Where(x => x.IsEditableOnUpdate).ToList());
    }

    private static bool IsSharedJoinEntity(IEntityType entityType)
        => entityType.ClrType == typeof(Dictionary<string, object>)
           && entityType.FindPrimaryKey()?.Properties.Count != 1;

    private static string GetRouteName(IEntityType entityType)
    {
        return (entityType.FindAnnotation("Relational:TableName")?.Value as string
                ?? entityType.ClrType.Name)
            .ToLowerInvariant();
    }

    private static bool IsEditableOnCreate(IProperty property)
    {
        if (!CanUserEdit(property))
        {
            return false;
        }

        return !property.IsPrimaryKey() || property.ValueGenerated == ValueGenerated.Never;
    }

    private static bool IsEditableOnUpdate(IProperty property)
        => !property.IsPrimaryKey()
           && CanUserEdit(property);

    private static bool CanUserEdit(IProperty property)
        => !property.IsShadowProperty()
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
