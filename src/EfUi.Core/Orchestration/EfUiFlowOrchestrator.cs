using System.Collections.Concurrent;
using System.Reflection;
using EfUi.Core.Binding;
using EfUi.Core.Crud;
using EfUi.Core.Metadata;
using EfUi.Core.Query;
using EfUi.Core.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EfUi.Core.Orchestration;

/// <summary>
/// Deep application module for preparing EF UI use cases.
/// </summary>
public sealed class EfUiFlowOrchestrator : IEfUiFlowOrchestrator
{
    private static readonly ConcurrentDictionary<Type, Func<DbContext, IReadOnlyList<object>>> ReadRowsAccessors = new();

    private readonly IEntityMetadataProvider _metadataProvider;
    private readonly IEntityCrudService _crudService;
    private readonly IHtmlPageRenderer _renderer;
    private readonly IScalarValueBinder _binder;
    private readonly EntityListQueryExecutor _listQueryExecutor;

    public EfUiFlowOrchestrator(
        IEntityMetadataProvider? metadataProvider = null,
        IEntityCrudService? crudService = null,
        IHtmlPageRenderer? renderer = null,
        IScalarValueBinder? binder = null)
    {
        _metadataProvider = metadataProvider ?? new EfEntityMetadataProvider();
        _crudService = crudService ?? new EntityCrudService(_metadataProvider, binder ?? new ScalarValueBinder());
        _renderer = renderer ?? new HtmlPageRenderer();
        _binder = binder ?? new ScalarValueBinder();
        _listQueryExecutor = new EntityListQueryExecutor();
    }

    public EntityDiscoveryResult GetDiscoveryResult(DbContext dbContext)
        => _metadataProvider.GetDiscoveryResult(dbContext);

    public EntityMetadata? FindEntityMetadata(EntityDiscoveryResult discovery, string routeName)
        => discovery.Entities.SingleOrDefault(entity => string.Equals(entity.RouteName, routeName, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<string> GetRenderableIssueMessages(EntityDiscoveryResult discovery, string? routeName = null)
        => discovery.Issues
            .Where(issue => issue.CanRender && (routeName is null || string.Equals(issue.RouteName, routeName, StringComparison.OrdinalIgnoreCase)))
            .Select(issue => routeName is null ? $"{issue.RouteName} — {issue.Message}" : issue.Message)
            .ToList();

    public IReadOnlyList<string> GetBlockingIssueMessages(EntityDiscoveryResult discovery, string? routeName = null)
        => discovery.Issues
            .Where(issue => !issue.CanRender && (routeName is null || string.Equals(issue.RouteName, routeName, StringComparison.OrdinalIgnoreCase)))
            .Select(issue => routeName is null ? $"{issue.RouteName} — {issue.Message}" : issue.Message)
            .ToList();

    public async Task<RenderedListView> BuildRenderedListViewAsync(
        string routePrefix,
        DbContext dbContext,
        EntityMetadata metadata,
        TableQuery query,
        IReadOnlyList<string>? parserErrors = null,
        IReadOnlyList<string>? warnings = null,
        bool includeAllRows = false,
        CancellationToken cancellationToken = default)
    {
        var executionQuery = includeAllRows ? query with { Limit = int.MaxValue } : query;
        var result = await _listQueryExecutor.ExecuteAsync(dbContext, metadata, executionQuery, cancellationToken).ConfigureAwait(false);
        if (includeAllRows)
        {
            result = result with { Offset = query.Offset, Limit = query.Limit };
        }

        return RenderedListViewFactory.Create(routePrefix, metadata, result, parserErrors, warnings);
    }

    public async Task<PreparedEntityForm?> PrepareFormAsync(
        DbContext dbContext,
        EntityMetadata metadata,
        object? key,
        bool isCreate,
        IReadOnlyDictionary<string, string[]>? submittedValues = null)
    {
        object? model = null;
        if (!isCreate)
        {
            if (key is null)
            {
                return null;
            }

            model = await dbContext.FindAsync(metadata.ClrType, key);
            if (model is null)
            {
                return null;
            }

            await LoadEditableCollectionsAsync(dbContext, metadata, model, isCreate: false);
        }

        return new PreparedEntityForm(
            model,
            key,
            BuildFieldOptions(dbContext, metadata, model, submittedValues, isCreate));
    }

    public object? TryReadKey(DbContext dbContext, EntityMetadata metadata, string rawKey)
    {
        if (dbContext.Model.FindEntityType(metadata.ClrType)?.FindPrimaryKey() is null)
        {
            return null;
        }

        var bindResult = _binder.Bind(metadata.PrimaryKeyProperty.ClrType, rawKey);
        return bindResult.IsSuccess ? bindResult.Value : null;
    }

    public IReadOnlyDictionary<string, string[]> EnsureCollectionFieldsPresent(
        EntityMetadata metadata,
        IReadOnlyDictionary<string, string[]> submittedValues,
        bool isCreate)
    {
        var values = submittedValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        var fields = isCreate ? metadata.CreateEditableFields : metadata.UpdateEditableFields;
        foreach (var field in fields.Where(field => field.Kind == EditableFieldKind.Collection && !values.ContainsKey(field.Name)))
        {
            values[field.Name] = [];
        }

        return values;
    }

    public Task<CrudOperationResult> CreateAsync(
        DbContext dbContext,
        string entityRoute,
        IReadOnlyDictionary<string, string?> values)
        => _crudService.CreateAsync(dbContext, entityRoute, values);

    public Task<CrudOperationResult> CreateAsync(
        DbContext dbContext,
        string entityRoute,
        IReadOnlyDictionary<string, string[]> values)
        => _crudService.CreateAsync(dbContext, entityRoute, values);

    public Task<CrudOperationResult> UpdateAsync(
        DbContext dbContext,
        string entityRoute,
        object key,
        IReadOnlyDictionary<string, string?> values)
        => _crudService.UpdateAsync(dbContext, entityRoute, key, values);

    public Task<CrudOperationResult> UpdateAsync(
        DbContext dbContext,
        string entityRoute,
        object key,
        IReadOnlyDictionary<string, string[]> values)
        => _crudService.UpdateAsync(dbContext, entityRoute, key, values);

    public Task<CrudOperationResult> DeleteAsync(
        DbContext dbContext,
        string entityRoute,
        object key)
        => _crudService.DeleteAsync(dbContext, entityRoute, key);

    public string RenderIndexPage(string routePrefix, EntityDiscoveryResult discovery)
        => _renderer.RenderIndex(
            routePrefix,
            discovery.Entities,
            GetRenderableIssueMessages(discovery),
            GetBlockingIssueMessages(discovery));

    public string RenderListPage(
        string routePrefix,
        EntityMetadata metadata,
        RenderedListView view,
        bool showActions,
        string? antiForgeryToken)
        => _renderer.RenderList(routePrefix, metadata, view, showActions, antiForgeryToken);

    public string RenderFormPage(
        string routePrefix,
        EntityMetadata metadata,
        PreparedEntityForm form,
        IReadOnlyDictionary<string, string[]> errors,
        IReadOnlyDictionary<string, string[]>? submittedValues,
        string? antiForgeryToken)
        => _renderer.RenderEditForm(
            routePrefix,
            metadata,
            form.Model,
            form.Key is null,
            errors,
            form.Key,
            submittedValues,
            form.FieldOptions,
            antiForgeryToken);

    public string RenderErrorPage(string routePrefix, string title, IReadOnlyList<string> messages)
        => _renderer.RenderErrorPage(routePrefix, title, messages);

    private static IReadOnlyList<object> ApplyTableQuery(
        IReadOnlyList<object> rows,
        EntityMetadata metadata,
        TableQuery query,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> relatedValueLookups,
        bool includeAllRows)
    {
        IEnumerable<object> filteredRows = rows;

        foreach (var filter in query.Filters)
        {
            var property = metadata.AllProperties.Single(candidate => candidate.Name == filter.Field);
            filteredRows = filteredRows.Where(row => MatchesFilter(row, property, filter, relatedValueLookups));
        }

        IOrderedEnumerable<object>? orderedRows = null;
        foreach (var sort in query.Sorts)
        {
            var property = metadata.AllProperties.Single(candidate => candidate.Name == sort.Field);
            Func<object, object?> keySelector = row => GetSortKeyValue(row, property, relatedValueLookups);
            var descending = string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase);

            if (orderedRows is null)
            {
                orderedRows = descending
                    ? filteredRows.OrderByDescending(keySelector, SortKeyComparer.Instance)
                    : filteredRows.OrderBy(keySelector, SortKeyComparer.Instance);
            }
            else
            {
                orderedRows = descending
                    ? orderedRows.ThenByDescending(keySelector, SortKeyComparer.Instance)
                    : orderedRows.ThenBy(keySelector, SortKeyComparer.Instance);
            }
        }

        return includeAllRows
            ? (orderedRows ?? filteredRows).ToList()
            : (orderedRows ?? filteredRows)
                .Skip(query.Offset)
                .Take(query.Limit)
                .ToList();
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> BuildRelatedValueLookups(
        DbContext dbContext,
        EntityMetadata metadata,
        RequestRowCache rowCache)
    {
        var entityType = dbContext.Model.FindEntityType(metadata.ClrType);
        if (entityType is null)
        {
            return new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        }

        var visiblePropertyNames = metadata.AllProperties
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        var lookups = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);

        foreach (var foreignKey in entityType.GetForeignKeys().Where(foreignKey => foreignKey.Properties.Count == 1))
        {
            var foreignKeyProperty = foreignKey.Properties[0];
            if (!visiblePropertyNames.Contains(foreignKeyProperty.Name))
            {
                continue;
            }

            var relatedPrimaryKey = foreignKey.PrincipalEntityType.FindPrimaryKey()?.Properties.SingleOrDefault();
            if (relatedPrimaryKey is null)
            {
                continue;
            }

            var relatedProperty = metadata.AllProperties.Single(property => property.Name == foreignKeyProperty.Name);
            lookups[foreignKeyProperty.Name] = rowCache.GetRows(dbContext, foreignKey.PrincipalEntityType.ClrType)
                .ToDictionary(
                    row => FormatValue(row.GetType().GetProperty(relatedPrimaryKey.Name)?.GetValue(row)),
                    row => GetRelatedEntityLabel(row, relatedPrimaryKey.Name, relatedProperty.RelatedDisplayPropertyName),
                    StringComparer.Ordinal);
        }

        return lookups;
    }

    private static IReadOnlyList<RenderedListRow> CreateRenderedListRows(
        string routePrefix,
        EntityMetadata metadata,
        IReadOnlyList<object> rows,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> relatedValueLookups)
        => rows.Select(row => new RenderedListRow(
            FormatValue(row.GetType().GetProperty(metadata.PrimaryKeyProperty.Name)?.GetValue(row)),
            metadata.AllProperties.ToDictionary(
                property => property.Name,
                property => CreateRenderedListCell(routePrefix, row, property, relatedValueLookups)))).ToList();

    private static RenderedListCell CreateRenderedListCell(
        string routePrefix,
        object row,
        EntityPropertyMetadata property,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> relatedValueLookups)
    {
        var rawValue = row.GetType().GetProperty(property.Name)?.GetValue(row);
        var formattedRawValue = FormatValue(rawValue);
        var text = GetRenderedListCellValue(row, property.Name, relatedValueLookups);
        var href = property.RelatedRouteName is not null
                   && !string.IsNullOrWhiteSpace(formattedRawValue)
                   && relatedValueLookups.TryGetValue(property.Name, out var lookup)
                   && lookup.ContainsKey(formattedRawValue)
            ? $"{routePrefix}/{property.RelatedRouteName}/{Uri.EscapeDataString(formattedRawValue)}/edit"
            : null;

        return new RenderedListCell(text, href);
    }

    private static bool MatchesFilter(
        object row,
        EntityPropertyMetadata property,
        TableFilterClause filter,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> relatedValueLookups)
    {
        var candidate = GetRenderedListCellValue(row, property.Name, relatedValueLookups);
        var rawValue = FormatValue(row.GetType().GetProperty(property.Name)?.GetValue(row));
        var filterValue = filter.Value ?? string.Empty;

        return filter.Operator.ToLowerInvariant() switch
        {
            "contains" => candidate.Contains(filterValue, StringComparison.OrdinalIgnoreCase),
            "eq" => string.Equals(candidate, filterValue, StringComparison.OrdinalIgnoreCase)
                || string.Equals(rawValue, filterValue, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static object? GetSortKeyValue(
        object row,
        EntityPropertyMetadata property,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> relatedValueLookups)
        => relatedValueLookups.ContainsKey(property.Name)
            ? GetRenderedListCellValue(row, property.Name, relatedValueLookups)
            : row.GetType().GetProperty(property.Name)?.GetValue(row);

    private static string GetRenderedListCellValue(
        object row,
        string propertyName,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> relatedValueLookups)
    {
        var rawValue = row.GetType().GetProperty(propertyName)?.GetValue(row);
        var formattedValue = FormatValue(rawValue);

        return relatedValueLookups.TryGetValue(propertyName, out var lookup)
               && lookup.TryGetValue(formattedValue, out var label)
            ? label
            : formattedValue;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<RelatedEntityOption>> BuildFieldOptions(
        DbContext dbContext,
        EntityMetadata metadata,
        object? model,
        IReadOnlyDictionary<string, string[]>? submittedValues,
        bool isCreate)
    {
        var rowCache = new RequestRowCache();
        var fields = isCreate ? metadata.CreateEditableFields : metadata.UpdateEditableFields;
        var oneToManyFields = fields
            .Where(field => field.Kind == EditableFieldKind.Collection
                && field.CollectionRelationshipKind == CollectionRelationshipKind.OneToMany
                && field.RelatedClrType is not null)
            .ToList();
        var ownerLabels = oneToManyFields.Count == 0
            ? null
            : rowCache.GetRows(dbContext, metadata.ClrType)
                .ToDictionary(
                    row => FormatValue(row.GetType().GetProperty(metadata.PrimaryKeyProperty.Name)?.GetValue(row)),
                    row => GetRelatedEntityLabel(row, metadata.PrimaryKeyProperty.Name),
                    StringComparer.Ordinal);

        var options = new Dictionary<string, IReadOnlyList<RelatedEntityOption>>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            if (field.Kind is not (EditableFieldKind.Reference or EditableFieldKind.Collection) || field.RelatedClrType is null)
            {
                continue;
            }

            var selectedValues = GetSelectedValues(dbContext, field, model, submittedValues);
            var relatedRows = rowCache.GetRows(dbContext, field.RelatedClrType);
            options[field.Name] = relatedRows
                .Select(row => CreateRelatedEntityOption(dbContext, metadata, field, row, selectedValues, model, ownerLabels))
                .ToList();
        }

        return options;
    }

    private static RelatedEntityOption CreateRelatedEntityOption(
        DbContext dbContext,
        EntityMetadata metadata,
        EditableFieldMetadata field,
        object row,
        HashSet<string> selectedValues,
        object? model,
        IReadOnlyDictionary<string, string>? ownerLabels)
    {
        var relatedClrType = field.RelatedClrType
            ?? throw new InvalidOperationException($"Field '{field.Name}' is missing a related entity type.");
        var entityType = dbContext.Model.FindEntityType(relatedClrType)
            ?? throw new InvalidOperationException($"Unknown related entity type '{relatedClrType.Name}'.");
        var primaryKey = entityType.FindPrimaryKey()?.Properties.SingleOrDefault()
            ?? throw new InvalidOperationException($"Entity '{relatedClrType.Name}' must have a single primary key.");

        var keyValue = row.GetType().GetProperty(primaryKey.Name)?.GetValue(row);
        var value = FormatValue(keyValue);
        var label = GetRelatedEntityLabel(row, primaryKey.Name, field.RelatedDisplayPropertyName);
        var selected = selectedValues.Contains(value);

        if (field.Kind == EditableFieldKind.Collection
            && field.CollectionRelationshipKind == CollectionRelationshipKind.OneToMany
            && field.ScalarPropertyName is not null
            && model is not null)
        {
            var ownerValue = row.GetType().GetProperty(field.ScalarPropertyName)?.GetValue(row);
            var currentParentKey = model.GetType().GetProperty(metadata.PrimaryKeyProperty.Name)?.GetValue(model);
            if (ownerValue is not null && !Equals(ownerValue, currentParentKey))
            {
                var ownerLabel = ownerLabels is not null && ownerLabels.TryGetValue(FormatValue(ownerValue), out var resolvedOwnerLabel)
                    ? resolvedOwnerLabel
                    : FormatValue(ownerValue);

                return new RelatedEntityOption(value, label, selected, Disabled: true, Description: $"assigned to {ownerLabel}");
            }
        }

        return new RelatedEntityOption(value, label, selected);
    }

    private static HashSet<string> GetSelectedValues(
        DbContext dbContext,
        EditableFieldMetadata field,
        object? model,
        IReadOnlyDictionary<string, string[]>? submittedValues)
    {
        if (submittedValues is not null && submittedValues.TryGetValue(field.Name, out var submittedValue))
        {
            return submittedValue
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.Ordinal);
        }

        if (model is null)
        {
            return [];
        }

        if (field.Kind == EditableFieldKind.Reference && field.ScalarPropertyName is not null)
        {
            var currentValue = model.GetType().GetProperty(field.ScalarPropertyName)?.GetValue(model);
            var formatted = FormatValue(currentValue);
            return string.IsNullOrWhiteSpace(formatted) ? [] : [formatted];
        }

        if (field.Kind == EditableFieldKind.Collection && field.NavigationPropertyName is not null && field.RelatedClrType is not null)
        {
            var collection = model.GetType().GetProperty(field.NavigationPropertyName)?.GetValue(model) as System.Collections.IEnumerable;
            if (collection is null)
            {
                return [];
            }

            var keyPropertyName = dbContext.Model.FindEntityType(field.RelatedClrType)?.FindPrimaryKey()?.Properties.SingleOrDefault()?.Name;
            if (keyPropertyName is null)
            {
                return [];
            }

            return collection.Cast<object>()
                .Select(item => item.GetType().GetProperty(keyPropertyName)?.GetValue(item))
                .Select(FormatValue)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.Ordinal);
        }

        return [];
    }

    private static async Task LoadEditableCollectionsAsync(DbContext dbContext, EntityMetadata metadata, object model, bool isCreate)
    {
        var fields = isCreate ? metadata.CreateEditableFields : metadata.UpdateEditableFields;
        foreach (var field in fields.Where(field => field.Kind == EditableFieldKind.Collection && field.NavigationPropertyName is not null))
        {
            await dbContext.Entry(model).Collection(field.NavigationPropertyName!).LoadAsync();
        }
    }

    private static IReadOnlyList<object> ReadRows(DbContext dbContext, Type entityClrType)
        => ReadRowsAccessors.GetOrAdd(entityClrType, CreateReadRowsAccessor)(dbContext);

    private static Func<DbContext, IReadOnlyList<object>> CreateReadRowsAccessor(Type entityClrType)
    {
        var method = typeof(EfUiFlowOrchestrator).GetMethod(nameof(ReadRowsCore), BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not resolve the row reader method.");

        return (Func<DbContext, IReadOnlyList<object>>)method
            .MakeGenericMethod(entityClrType)
            .CreateDelegate(typeof(Func<DbContext, IReadOnlyList<object>>));
    }

    private static IReadOnlyList<object> ReadRowsCore<TEntity>(DbContext dbContext)
        where TEntity : class
        => dbContext.Set<TEntity>().Cast<object>().ToList();

    private static string GetRelatedEntityLabel(object row, string primaryKeyPropertyName, string? displayPropertyName = null)
        => EntityDisplayLabelResolver.Resolve(row, displayPropertyName, primaryKeyPropertyName);

    private static string FormatValue(object? value)
        => value switch
        {
            null => string.Empty,
            DateTime dateTime => dateTime.ToString("O"),
            _ => value.ToString() ?? string.Empty
        };

    private sealed class RequestRowCache
    {
        private readonly Dictionary<Type, IReadOnlyList<object>> _rows = new();

        public IReadOnlyList<object> GetRows(DbContext dbContext, Type entityClrType)
        {
            if (!_rows.TryGetValue(entityClrType, out var rows))
            {
                rows = ReadRows(dbContext, entityClrType);
                _rows[entityClrType] = rows;
            }

            return rows;
        }
    }

    private sealed class SortKeyComparer : IComparer<object?>
    {
        internal static SortKeyComparer Instance { get; } = new();

        public int Compare(object? x, object? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            if (x is string leftString && y is string rightString)
            {
                return StringComparer.OrdinalIgnoreCase.Compare(leftString, rightString);
            }

            if (x is IComparable comparable && x.GetType() == y.GetType())
            {
                return comparable.CompareTo(y);
            }

            return StringComparer.OrdinalIgnoreCase.Compare(FormatValue(x), FormatValue(y));
        }
    }
}
