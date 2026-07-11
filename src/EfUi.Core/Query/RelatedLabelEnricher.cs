using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using EfUi.Core.Metadata;
using EfUi.Core.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfUi.Core.Query;

/// <summary>Enriches a fetched entity window with labels from only the related keys in that window.</summary>
internal sealed class RelatedLabelEnricher
{
    public async Task<RelatedLabelEnrichmentResult> EnrichAsync(
        DbContext dbContext,
        IReadOnlyList<object> entities,
        EntityMetadata metadata,
        IReadOnlyList<EntityListQueryRow> rows,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(rows);

        var enrichedRows = rows.ToList();
        var errors = new List<EntityListQueryError>();
        var dependentEntityType = dbContext.Model.FindEntityType(metadata.ClrType);
        if (dependentEntityType is null)
        {
            return new(enrichedRows, errors);
        }

        var contexts = new List<RelatedLabelContext>();
        foreach (var property in metadata.AllProperties.Where(property => property.RelatedClrType is not null))
        {
            var foreignKey = dependentEntityType.GetForeignKeys()
                .Where(candidate => candidate.Properties.Count == 1)
                .SingleOrDefault(candidate => candidate.Properties[0].Name == property.Name);
            var principalKey = foreignKey?.PrincipalEntityType.FindPrimaryKey()?.Properties.SingleOrDefault();
            if (foreignKey is null || principalKey is null || principalKey.PropertyInfo is null)
            {
                errors.Add(CreateUnsupportedError(property.Name, "requires a supported single-column related key."));
                continue;
            }

            var foreignKeyProperty = metadata.ClrType.GetProperty(property.Name);
            var displayProperty = RelatedQueryPropertyResolver.Find(dependentEntityType, property);
            var displayClrProperty = string.IsNullOrWhiteSpace(property.RelatedDisplayPropertyName)
                ? null
                : foreignKey.PrincipalEntityType.ClrType.GetProperty(property.RelatedDisplayPropertyName);
            if (foreignKeyProperty is null || (displayProperty is null && displayClrProperty is null))
            {
                errors.Add(CreateUnsupportedError(property.Name, "requires a mapped related relationship."));
                continue;
            }

            contexts.Add(new RelatedLabelContext(
                property,
                foreignKeyProperty,
                foreignKey.PrincipalEntityType,
                principalKey));
        }

        foreach (var contextGroup in contexts.GroupBy(context =>
                     (context.PrincipalEntityType.ClrType, context.PrincipalKey.Name)))
        {
            var firstContext = contextGroup.First();
            var keyValues = contextGroup
                .SelectMany(context => entities
                    .Select(entity => context.ForeignKeyProperty.GetValue(entity))
                    .Where(value => value is not null)
                    .Select(value => ConvertKey(value!, firstContext.PrincipalKey.ClrType)))
                .Distinct()
                .ToList();

            var relatedRows = keyValues.Count == 0
                ? []
                : await QueryRelatedRowsAsync(
                    dbContext,
                    firstContext.PrincipalEntityType,
                    firstContext.PrincipalKey,
                    keyValues,
                    cancellationToken).ConfigureAwait(false);

            foreach (var context in contextGroup)
            {
                var labels = relatedRows.ToDictionary(
                    related => context.PrincipalKey.PropertyInfo!.GetValue(related)!,
                    related => (string?)EntityDisplayLabelResolver.Resolve(
                        related,
                        context.Property.RelatedDisplayPropertyName,
                        context.PrincipalKey.Name));
                ApplyFallbacks(enrichedRows, entities, context.ForeignKeyProperty, context.Property.Name, labels);
            }
        }

        return new(enrichedRows, errors);
    }

    private static EntityListQueryError CreateUnsupportedError(string fieldName, string reason)
        => new(
            "unsupported-related-query-field",
            $"Field '{fieldName}' {reason}",
            fieldName);

    private static async Task<List<object>> QueryRelatedRowsAsync(
        DbContext dbContext,
        IEntityType principalEntityType,
        IProperty principalKey,
        IReadOnlyList<object> keys,
        CancellationToken cancellationToken)
    {
        var relatedSet = GetEntitySet(dbContext, principalEntityType.ClrType);
        var parameter = Expression.Parameter(principalEntityType.ClrType, "related");
        var keyMember = Expression.Property(parameter, principalKey.PropertyInfo!);
        var keyArray = Array.CreateInstance(principalKey.ClrType, keys.Count);
        for (var index = 0; index < keys.Count; index++)
        {
            keyArray.SetValue(keys[index], index);
        }

        var contains = Expression.Call(
            typeof(Enumerable),
            nameof(Enumerable.Contains),
            [principalKey.ClrType],
            Expression.Constant(keyArray),
            keyMember);
        var predicate = Expression.Lambda(contains, parameter);
        var query = relatedSet.Provider.CreateQuery(Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Where),
            [principalEntityType.ClrType],
            relatedSet.Expression,
            Expression.Quote(predicate)));
        return await ToListAsync(query, principalEntityType.ClrType, cancellationToken).ConfigureAwait(false);
    }

    private static IQueryable GetEntitySet(DbContext dbContext, Type entityType)
    {
        var method = typeof(DbContext)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(method => method.Name == nameof(DbContext.Set)
                && method.IsGenericMethodDefinition
                && method.GetParameters().Length == 0);
        return (IQueryable)method.MakeGenericMethod(entityType).Invoke(dbContext, null)!;
    }

    private static void ApplyFallbacks(
        IList<EntityListQueryRow> rows,
        IReadOnlyList<object> entities,
        PropertyInfo foreignKeyProperty,
        string fieldName,
        IReadOnlyDictionary<object, string?> labels)
    {
        for (var index = 0; index < entities.Count && index < rows.Count; index++)
        {
            var rawValue = foreignKeyProperty.GetValue(entities[index]);
            string? relatedLabel = null;
            var hasRelatedLabel = rawValue is not null && labels.TryGetValue(rawValue, out relatedLabel);
            var label = rawValue is null
                ? string.Empty
                : hasRelatedLabel
                    ? relatedLabel ?? string.Empty
                    : Format(rawValue) ?? string.Empty;
            var cell = rows[index].Cells[fieldName];
            var cells = rows[index].Cells.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            cells[fieldName] = cell with
            {
                DisplayText = label,
                RelatedRouteName = hasRelatedLabel ? cell.RelatedRouteName : null
            };
            rows[index] = rows[index] with { Cells = cells };
        }
    }

    private static object ConvertKey(object value, Type targetType)
    {
        var sourceType = value.GetType();
        if (sourceType == targetType)
        {
            return value;
        }

        if (targetType.IsEnum)
        {
            return Enum.ToObject(targetType, value);
        }

        if (targetType == typeof(Guid))
        {
            return value is Guid guid ? guid : Guid.Parse(value.ToString()!);
        }

        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture)!;
    }

    private static string? Format(object? value)
        => value switch
        {
            null => null,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

    private static async Task<List<object>> ToListAsync(IQueryable source, Type entityType, CancellationToken cancellationToken)
    {
        var method = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == nameof(EntityFrameworkQueryableExtensions.ToListAsync)
                && method.IsGenericMethodDefinition
                && method.GetParameters().Length == 2
                && method.GetParameters()[1].ParameterType == typeof(CancellationToken));
        var task = (Task)method.MakeGenericMethod(entityType).Invoke(null, [source, cancellationToken])!;
        await task.ConfigureAwait(false);
        var value = task.GetType().GetProperty("Result")!.GetValue(task)!;
        return ((IEnumerable)value).Cast<object>().ToList();
    }

    private sealed record RelatedLabelContext(
        EntityPropertyMetadata Property,
        PropertyInfo ForeignKeyProperty,
        IEntityType PrincipalEntityType,
        IProperty PrincipalKey);
}

internal sealed record RelatedLabelEnrichmentResult(
    IReadOnlyList<EntityListQueryRow> Rows,
    IReadOnlyList<EntityListQueryError> Errors);
