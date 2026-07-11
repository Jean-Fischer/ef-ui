using System.Globalization;
using System.Linq.Expressions;
using EfUi.Core.Metadata;
using EfUi.Core.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EfUi.Core.Query;

/// <summary>Executes validated list queries through the configured EF provider.</summary>
internal sealed class EntityListQueryExecutor
{
    private readonly EntityListQueryValidator _validator;
    private readonly ProviderQueryExpressionBuilder _expressionBuilder;
    private readonly Func<IQueryable, Type, CancellationToken, Task<List<object>>> _materializeAsync;
    private readonly RelatedLabelEnricher _relatedLabelEnricher;

    public EntityListQueryExecutor()
        : this(new EntityListQueryValidator(), new ProviderQueryExpressionBuilder())
    {
    }

    internal EntityListQueryExecutor(
        EntityListQueryValidator validator,
        ProviderQueryExpressionBuilder expressionBuilder,
        Func<IQueryable, Type, CancellationToken, Task<List<object>>>? materializeAsync = null)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _expressionBuilder = expressionBuilder ?? throw new ArgumentNullException(nameof(expressionBuilder));
        _materializeAsync = materializeAsync ?? EfQueryReflection.ToListAsync;
        _relatedLabelEnricher = new RelatedLabelEnricher();
    }

    public async Task<EntityListQueryResult> ExecuteAsync(
        DbContext dbContext,
        EntityMetadata metadata,
        TableQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(query);

        var validation = _validator.Validate(dbContext, metadata, query);
        var errors = validation.Errors.ToList();
        var appliedFilters = new List<TableFilterClause>();
        var appliedSorts = new List<TableSortClause>();

        if (query.Offset < 0)
        {
            errors.Add(new EntityListQueryError("invalid-offset", "Offset cannot be negative."));
        }

        if (query.Limit < 0)
        {
            errors.Add(new EntityListQueryError("invalid-limit", "Limit cannot be negative."));
        }

        if (query.Offset < 0 || query.Limit < 0)
        {
            return EmptyResult(query, appliedFilters, appliedSorts, errors);
        }

        var properties = metadata.AllProperties.ToDictionary(property => property.Name, StringComparer.Ordinal);
        var source = EfQueryReflection.GetEntitySet(dbContext, metadata.ClrType);
        var queryable = source;

        foreach (var filter in validation.AppliedFilters)
        {
            if (!properties.TryGetValue(filter.Field, out var property))
            {
                continue;
            }

            var built = _expressionBuilder.BuildFilter(dbContext, metadata.ClrType, property, filter);
            if (built.Error is not null)
            {
                errors.Add(built.Error);
                continue;
            }

            queryable = ApplyWhere(queryable, metadata.ClrType, built.Expression!);
            appliedFilters.Add(filter);
        }

        var sortExpressions = new List<(TableSortClause Clause, LambdaExpression Expression)>();
        foreach (var sort in validation.AppliedSorts)
        {
            if (!properties.TryGetValue(sort.Field, out var property))
            {
                continue;
            }

            var built = _expressionBuilder.BuildSort(dbContext, metadata.ClrType, property, sort);
            if (built.Error is not null)
            {
                errors.Add(built.Error);
                continue;
            }

            sortExpressions.Add((sort, built.Expression!));
            appliedSorts.Add(sort);
        }

        var keySort = new TableSortClause(metadata.PrimaryKeyProperty.Name, "asc");
        var keyExpression = _expressionBuilder.BuildSort(dbContext, metadata.ClrType, metadata.PrimaryKeyProperty, keySort);
        if (keyExpression.Error is not null)
        {
            errors.Add(keyExpression.Error);
        }
        else
        {
            if (sortExpressions.Count == 0)
            {
                queryable = ApplyOrdering(queryable, metadata.ClrType, keyExpression.Expression!, descending: false, thenBy: false);
            }
            else
            {
                for (var index = 0; index < sortExpressions.Count; index++)
                {
                    var sort = sortExpressions[index];
                    queryable = ApplyOrdering(
                        queryable,
                        metadata.ClrType,
                        sort.Expression,
                        descending: string.Equals(sort.Clause.Direction, "desc", StringComparison.OrdinalIgnoreCase),
                        thenBy: index > 0);
                }

                queryable = ApplyOrdering(queryable, metadata.ClrType, keyExpression.Expression!, descending: false, thenBy: true);
            }
        }

        if (query.Offset > 0)
        {
            queryable = ApplyPaging(queryable, metadata.ClrType, nameof(Queryable.Skip), query.Offset);
        }

        queryable = ApplyPaging(queryable, metadata.ClrType, nameof(Queryable.Take), query.Limit);

        List<object> entities;
        try
        {
            entities = await _materializeAsync(queryable, metadata.ClrType, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException exception) when (IsProviderTranslationFailure(exception))
        {
            errors.Add(new EntityListQueryError(
                "provider-translation-failure",
                "The provider could not translate the requested list query."));
            return EmptyResult(query, appliedFilters, appliedSorts, errors);
        }

        var projectedRows = entities.Select(entity => ProjectRow(entity, metadata)).ToList();
        try
        {
            var enrichment = await _relatedLabelEnricher
                .EnrichAsync(dbContext, entities, metadata, projectedRows, cancellationToken)
                .ConfigureAwait(false);
            if (enrichment.Errors.Count > 0)
            {
                errors.AddRange(enrichment.Errors);
                return EmptyResult(query, appliedFilters, appliedSorts, errors);
            }

            projectedRows = enrichment.Rows.ToList();
        }
        catch (InvalidOperationException exception) when (IsProviderTranslationFailure(exception))
        {
            errors.Add(new EntityListQueryError(
                "provider-translation-failure",
                "The provider could not translate the requested list query."));
            return EmptyResult(query, appliedFilters, appliedSorts, errors);
        }

        return new EntityListQueryResult(
            projectedRows,
            appliedFilters,
            appliedSorts,
            errors,
            offset: query.Offset,
            limit: query.Limit);
    }

    private static IQueryable ApplyWhere(IQueryable source, Type entityType, LambdaExpression predicate)
        => source.Provider.CreateQuery(Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Where),
            [entityType],
            source.Expression,
            Expression.Quote(predicate)));

    private static IQueryable ApplyOrdering(IQueryable source, Type entityType, LambdaExpression keySelector, bool descending, bool thenBy)
    {
        var methodName = thenBy
            ? descending ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy)
            : descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy);

        return source.Provider.CreateQuery(Expression.Call(
            typeof(Queryable),
            methodName,
            [entityType, keySelector.ReturnType],
            source.Expression,
            Expression.Quote(keySelector)));
    }

    private static IQueryable ApplyPaging(IQueryable source, Type entityType, string methodName, int amount)
        => source.Provider.CreateQuery(Expression.Call(
            typeof(Queryable),
            methodName,
            [entityType],
            source.Expression,
            Expression.Constant(amount)));

    private static EntityListQueryRow ProjectRow(object entity, EntityMetadata metadata)
    {
        var cells = new Dictionary<string, EntityListQueryCell>(StringComparer.Ordinal);
        foreach (var property in metadata.AllProperties)
        {
            var propertyInfo = metadata.ClrType.GetProperty(property.Name);
            var value = propertyInfo?.GetValue(entity);
            var text = Format(value);
            cells[property.Name] = new EntityListQueryCell(
                text,
                text ?? string.Empty,
                property.RelatedRouteName);
        }

        var keyValue = metadata.ClrType.GetProperty(metadata.PrimaryKeyProperty.Name)?.GetValue(entity);
        return new EntityListQueryRow(Format(keyValue) ?? string.Empty, cells);
    }

    private static string? Format(object? value)
        => value switch
        {
            null => null,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

    private static EntityListQueryResult EmptyResult(
        TableQuery query,
        IReadOnlyList<TableFilterClause> appliedFilters,
        IReadOnlyList<TableSortClause> appliedSorts,
        IReadOnlyList<EntityListQueryError> errors)
        => new([], appliedFilters, appliedSorts, errors, offset: query.Offset, limit: query.Limit);

    private static bool IsProviderTranslationFailure(InvalidOperationException exception)
    {
        var message = exception.ToString();
        return message.Contains("could not be translated", StringComparison.OrdinalIgnoreCase)
            || message.Contains("translation failed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("cannot be translated", StringComparison.OrdinalIgnoreCase);
    }
}
