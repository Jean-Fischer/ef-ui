using System.Linq.Expressions;
using EfUi.Core.Binding;
using EfUi.Core.Metadata;
using EfUi.Core.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfUi.Core.Query;

/// <summary>Builds provider expressions over mapped scalar and supported one-hop related properties.</summary>
internal sealed class ProviderQueryExpressionBuilder
{
    private readonly ScalarValueBinder _valueBinder = new();

    public ProviderQueryExpressionBuildResult BuildFilter(
        Type entityType,
        EntityPropertyMetadata property,
        TableFilterClause clause)
        => BuildFilter(null, entityType, property, clause);

    public ProviderQueryExpressionBuildResult BuildFilter(
        DbContext? dbContext,
        Type entityType,
        EntityPropertyMetadata property,
        TableFilterClause clause)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(clause);

        var parameter = Expression.Parameter(entityType, "entity");
        Expression member;
        Type valueType;
        if (property.RelatedDisplayPropertyName is not null)
        {
            if (dbContext is null || !TryBuildRelatedDisplayExpression(dbContext, entityType, property, parameter, out member, out valueType))
            {
                return Failure("unsupported-related-query-field", $"Field '{clause.Field}' requires related-label query support.", clause.Field);
            }
        }
        else
        {
            var propertyInfo = entityType.GetProperty(property.Name);
            if (propertyInfo is null)
            {
                return Failure("unsupported-filter-field", $"Property '{property.Name}' is not mapped on '{entityType.Name}'.", clause.Field);
            }

            member = Expression.Property(parameter, propertyInfo);
            valueType = propertyInfo.PropertyType;
        }

        if (!string.Equals(clause.Operator, "eq", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(clause.Operator, "contains", StringComparison.OrdinalIgnoreCase))
        {
            return Failure("unsupported-filter-operator", $"Unsupported filter operator '{clause.Operator}' for field '{clause.Field}'.", clause.Field);
        }

        if (string.Equals(clause.Operator, "contains", StringComparison.OrdinalIgnoreCase)
            && valueType != typeof(string))
        {
            return Failure("unsupported-filter-operator", $"Operator 'contains' is only supported for string field '{clause.Field}'.", clause.Field);
        }

        var binding = _valueBinder.Bind(valueType, clause.Value);
        if (!binding.IsSuccess)
        {
            return Failure("invalid-filter-value", binding.Error ?? $"Invalid value for field '{clause.Field}'.", clause.Field);
        }

        var constant = CreateTypedConstant(binding.Value, valueType);
        Expression body = string.Equals(clause.Operator, "contains", StringComparison.OrdinalIgnoreCase)
            ? Expression.Call(member, nameof(string.Contains), Type.EmptyTypes, constant)
            : Expression.Equal(member, constant);

        return new ProviderQueryExpressionBuildResult(Expression.Lambda(body, parameter), null);
    }

    public ProviderQueryExpressionBuildResult BuildSort(
        Type entityType,
        EntityPropertyMetadata property,
        TableSortClause clause)
        => BuildSort(null, entityType, property, clause);

    public ProviderQueryExpressionBuildResult BuildSort(
        DbContext? dbContext,
        Type entityType,
        EntityPropertyMetadata property,
        TableSortClause clause)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(clause);

        var parameter = Expression.Parameter(entityType, "entity");
        Expression member;
        if (property.RelatedDisplayPropertyName is not null)
        {
            if (dbContext is null || !TryBuildRelatedDisplayExpression(dbContext, entityType, property, parameter, out member, out _))
            {
                return Failure("unsupported-related-query-field", $"Field '{clause.Field}' requires related-label query support.", clause.Field);
            }
        }
        else
        {
            var propertyInfo = entityType.GetProperty(property.Name);
            if (propertyInfo is null)
            {
                return Failure("unsupported-sort-field", $"Property '{property.Name}' is not mapped on '{entityType.Name}'.", clause.Field);
            }

            member = Expression.Property(parameter, propertyInfo);
        }

        return new ProviderQueryExpressionBuildResult(Expression.Lambda(member, parameter), null);
    }

    private static bool TryBuildRelatedDisplayExpression(
        DbContext dbContext,
        Type entityType,
        EntityPropertyMetadata property,
        ParameterExpression parameter,
        out Expression expression,
        out Type valueType)
    {
        expression = null!;
        valueType = null!;

        var dependentEntityType = dbContext.Model.FindEntityType(entityType);
        var foreignKey = dependentEntityType?.GetForeignKeys()
            .Where(candidate => candidate.Properties.Count == 1)
            .SingleOrDefault(candidate => candidate.Properties[0].Name == property.Name);
        var principalKey = foreignKey?.PrincipalEntityType.FindPrimaryKey()?.Properties.SingleOrDefault();
        var displayProperty = foreignKey?.PrincipalEntityType.FindProperty(property.RelatedDisplayPropertyName!);
        if (foreignKey is null
            || principalKey is null
            || principalKey.PropertyInfo is null
            || displayProperty is null
            || displayProperty.PropertyInfo is null)
        {
            return false;
        }

        valueType = displayProperty.ClrType;
        var navigationInfo = foreignKey.DependentToPrincipal?.PropertyInfo;
        if (navigationInfo is not null)
        {
            var navigation = Expression.Property(parameter, navigationInfo);
            expression = Expression.Property(navigation, displayProperty.PropertyInfo);
            return true;
        }

        var relatedSet = GetEntitySet(dbContext, foreignKey.PrincipalEntityType.ClrType);
        var relatedParameter = Expression.Parameter(foreignKey.PrincipalEntityType.ClrType, "related");
        var relatedKey = Expression.Property(relatedParameter, principalKey.PropertyInfo);
        var foreignKeyProperty = entityType.GetProperty(property.Name);
        if (foreignKeyProperty is null)
        {
            return false;
        }

        var foreignKeyValue = Expression.Property(parameter, foreignKeyProperty);
        var comparableForeignKey = ConvertForEquality(foreignKeyValue, relatedKey.Type);
        var keyPredicate = Expression.Lambda(
            Expression.Equal(relatedKey, comparableForeignKey),
            relatedParameter);
        var filtered = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Where),
            [foreignKey.PrincipalEntityType.ClrType],
            relatedSet.Expression,
            Expression.Quote(keyPredicate));
        var displaySelector = Expression.Lambda(
            Expression.Property(relatedParameter, displayProperty.PropertyInfo),
            relatedParameter);
        var selected = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Select),
            [foreignKey.PrincipalEntityType.ClrType, displayProperty.ClrType],
            filtered,
            Expression.Quote(displaySelector));
        expression = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.FirstOrDefault),
            [displayProperty.ClrType],
            selected);
        return true;
    }

    private static IQueryable GetEntitySet(DbContext dbContext, Type entityType)
    {
        var method = typeof(DbContext)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Single(method => method.Name == nameof(DbContext.Set)
                && method.IsGenericMethodDefinition
                && method.GetParameters().Length == 0);
        return (IQueryable)method.MakeGenericMethod(entityType).Invoke(dbContext, null)!;
    }

    private static Expression ConvertForEquality(Expression value, Type targetType)
    {
        if (value.Type == targetType)
        {
            return value;
        }

        if (Nullable.GetUnderlyingType(value.Type) == targetType)
        {
            return Expression.Property(value, nameof(Nullable<int>.Value));
        }

        return Expression.Convert(value, targetType);
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
