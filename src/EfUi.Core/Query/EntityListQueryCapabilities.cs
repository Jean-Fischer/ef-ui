using EfUi.Core.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfUi.Core.Query;

/// <summary>Describes the provider-queryable capabilities of the fields rendered for an entity.</summary>
internal sealed class EntityListQueryCapabilities
{
    private EntityListQueryCapabilities(IReadOnlyDictionary<string, EntityListQueryFieldCapabilities> fields)
    {
        Fields = fields;
    }

    public IReadOnlyDictionary<string, EntityListQueryFieldCapabilities> Fields { get; }

    public static EntityListQueryCapabilities Create(DbContext dbContext, EntityMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(metadata);

        return Create(dbContext.Model, metadata);
    }

    public static EntityListQueryCapabilities Create(IModel model, EntityMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(metadata);

        var entityType = model.FindEntityType(metadata.ClrType);
        var fields = new Dictionary<string, EntityListQueryFieldCapabilities>(StringComparer.Ordinal);

        foreach (var property in metadata.AllProperties)
        {
            var mappedProperty = entityType?.FindProperty(property.Name);
            var relatedDisplayProperty = entityType is null
                ? null
                : RelatedQueryPropertyResolver.Find(entityType, property);
            var isRelatedProperty = entityType?.GetForeignKeys()
                .Any(foreignKey => foreignKey.Properties.Count == 1 && foreignKey.Properties[0].Name == property.Name)
                == true;
            var isDisplayOnly = isRelatedProperty
                && property.RelatedDisplayPropertyName is not null
                && relatedDisplayProperty is null;
            var effectiveType = relatedDisplayProperty?.ClrType ?? mappedProperty?.ClrType ?? property.ClrType;
            var queryable = mappedProperty is not null
                && (!isRelatedProperty || relatedDisplayProperty is not null)
                && !isDisplayOnly;
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

internal sealed record EntityListQueryFieldCapabilities(
    string Name,
    Type ClrType,
    bool IsFilterable,
    bool IsSortable,
    bool IsDisplayOnly,
    IReadOnlyList<string> SupportedOperators);
