using System.Linq.Expressions;
using EfUi.Core.Binding;
using EfUi.Core.Metadata;
using EfUi.Core.Rendering;

namespace EfUi.Core.Query;

/// <summary>Builds expressions over directly mapped scalar entity properties.</summary>
internal sealed class ProviderQueryExpressionBuilder
{
    private readonly ScalarValueBinder _valueBinder = new();

    public ProviderQueryExpressionBuildResult BuildFilter(
        Type entityType,
        EntityPropertyMetadata property,
        TableFilterClause clause)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(clause);

        if (property.RelatedDisplayPropertyName is not null)
        {
            return Failure("unsupported-related-query-field", $"Field '{clause.Field}' requires related-label query support.", clause.Field);
        }

        var propertyInfo = entityType.GetProperty(property.Name);
        if (propertyInfo is null)
        {
            return Failure("unsupported-filter-field", $"Property '{property.Name}' is not mapped on '{entityType.Name}'.", clause.Field);
        }

        if (!string.Equals(clause.Operator, "eq", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(clause.Operator, "contains", StringComparison.OrdinalIgnoreCase))
        {
            return Failure("unsupported-filter-operator", $"Unsupported filter operator '{clause.Operator}' for field '{clause.Field}'.", clause.Field);
        }

        if (string.Equals(clause.Operator, "contains", StringComparison.OrdinalIgnoreCase)
            && propertyInfo.PropertyType != typeof(string))
        {
            return Failure("unsupported-filter-operator", $"Operator 'contains' is only supported for string field '{clause.Field}'.", clause.Field);
        }

        var binding = _valueBinder.Bind(propertyInfo.PropertyType, clause.Value);
        if (!binding.IsSuccess)
        {
            return Failure("invalid-filter-value", binding.Error ?? $"Invalid value for field '{clause.Field}'.", clause.Field);
        }

        var parameter = Expression.Parameter(entityType, "entity");
        var member = Expression.Property(parameter, propertyInfo);
        var constant = CreateTypedConstant(binding.Value, propertyInfo.PropertyType);
        Expression body = string.Equals(clause.Operator, "contains", StringComparison.OrdinalIgnoreCase)
            ? Expression.Call(member, nameof(string.Contains), Type.EmptyTypes, constant)
            : Expression.Equal(member, constant);

        return new ProviderQueryExpressionBuildResult(Expression.Lambda(body, parameter), null);
    }

    public ProviderQueryExpressionBuildResult BuildSort(
        Type entityType,
        EntityPropertyMetadata property,
        TableSortClause clause)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(clause);

        if (property.RelatedDisplayPropertyName is not null)
        {
            return Failure("unsupported-related-query-field", $"Field '{clause.Field}' requires related-label query support.", clause.Field);
        }

        var propertyInfo = entityType.GetProperty(property.Name);
        if (propertyInfo is null)
        {
            return Failure("unsupported-sort-field", $"Property '{property.Name}' is not mapped on '{entityType.Name}'.", clause.Field);
        }

        var parameter = Expression.Parameter(entityType, "entity");
        var member = Expression.Property(parameter, propertyInfo);
        return new ProviderQueryExpressionBuildResult(Expression.Lambda(member, parameter), null);
    }

    private static Expression CreateTypedConstant(object? value, Type targetType)
    {
        var holderType = typeof(TypedValue<>).MakeGenericType(targetType);
        var holder = Activator.CreateInstance(holderType)!;
        holderType.GetProperty(nameof(TypedValue<int>.Value))!.SetValue(holder, value);
        return Expression.Property(Expression.Constant(holder, holderType), nameof(TypedValue<int>.Value));
    }

    private sealed class TypedValue<T>
    {
        public T? Value { get; set; }
    }

    private static ProviderQueryExpressionBuildResult Failure(string code, string message, string? field)
        => new(null, new EntityListQueryError(code, message, field));
}

internal sealed record ProviderQueryExpressionBuildResult(
    LambdaExpression? Expression,
    EntityListQueryError? Error);
