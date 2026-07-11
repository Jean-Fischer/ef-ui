using EfUi.Core.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfUi.Core.Query;

/// <summary>Describes the provider-queryable capabilities of the fields rendered for an entity.</summary>
public sealed class EntityListQueryCapabilities
{
    private EntityListQueryCapabilities(IReadOnlyDictionary<string, EntityListQueryFieldCapabilities> fields)
    {
        Fields = fields;
    }

    public IReadOnlyDictionary<string, EntityListQueryFieldCapabilities> Fields { get; }

    public static EntityListQueryCapabilities Create(DbContext dbContext, EntityMetadata metadata)
        => Create(dbContext.Model, metadata);

    public static EntityListQueryCapabilities Create(IModel model, EntityMetadata metadata)
    {
        var entityType = model.FindEntityType(metadata.ClrType);
        var fields = new Dictionary<string, EntityListQueryFieldCapabilities>(StringComparer.Ordinal);

        foreach (var property in metadata.AllProperties)
        {
            var mappedProperty = entityType?.FindProperty(property.Name);
            var relatedDisplayProperty = FindRelatedDisplayProperty(model, property, entityType);
            var isDisplayOnly = property.RelatedDisplayPropertyName is not null
                && relatedDisplayProperty is null;
            var effectiveType = relatedDisplayProperty?.ClrType ?? mappedProperty?.ClrType ?? property.ClrType;
            var queryable = mappedProperty is not null && !isDisplayOnly;
            IReadOnlyList<string> operators = queryable ? GetSupportedOperators(effectiveType) : [];

            fields[property.Name] = new EntityListQueryFieldCapabilities(
                property.Name,
                effectiveType,
                queryable,
                queryable,
                isDisplayOnly,
                operators);
        }

        return new EntityListQueryCapabilities(fields);
    }

    private static IProperty? FindRelatedDisplayProperty(IModel model, EntityPropertyMetadata property, IEntityType? dependentEntityType)
    {
        if (property.RelatedDisplayPropertyName is null || dependentEntityType is null)
        {
            return null;
        }

        var foreignKey = dependentEntityType.GetForeignKeys()
            .Where(foreignKey => foreignKey.Properties.Count == 1)
            .SingleOrDefault(foreignKey => foreignKey.Properties[0].Name == property.Name);
        if (foreignKey is null)
        {
            return null;
        }

        return foreignKey.PrincipalEntityType.FindProperty(property.RelatedDisplayPropertyName);
    }

    private static IReadOnlyList<string> GetSupportedOperators(Type type)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        var operators = new List<string> { "eq" };
        if (actualType == typeof(string))
        {
            operators.Insert(0, "contains");
        }

        return operators;
    }
}

public sealed record EntityListQueryFieldCapabilities(
    string Name,
    Type ClrType,
    bool IsFilterable,
    bool IsSortable,
    bool IsDisplayOnly,
    IReadOnlyList<string> SupportedOperators);
