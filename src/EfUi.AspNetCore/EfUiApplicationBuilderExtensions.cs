using System.Security.Claims;
using System.Text.Json;
using EfUi.Core.Binding;
using EfUi.Core.Crud;
using EfUi.Core.Metadata;
using EfUi.Core.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EfUi.AspNetCore;

public static class EfUiApplicationBuilderExtensions
{
    private const string HtmlContentType = "text/html";

    public static WebApplication UseEfUi(this WebApplication app, Action<EfUiOptions> configure)
    {
        var options = new EfUiOptions();
        configure(options);

        if (!options.EnableInProduction && app.Environment.IsProduction())
        {
            return app;
        }

        MapEfUiRoutes(app, options);
        return app;
    }

    private static void MapEfUiRoutes(WebApplication app, EfUiOptions options)
    {
        RequireBrowserAuthorization(app.MapGet($"{options.RoutePrefix}/assets/efui.css", ()
            => Results.Text(EfUiFormCss.Content, "text/css")), options);

        RequireBrowserAuthorization(app.MapGet($"{options.RoutePrefix}/assets/efui-table.css", ()
            => Results.Text(EfUiTableAssets.StylesheetContent, "text/css")), options);

        RequireBrowserAuthorization(app.MapGet($"{options.RoutePrefix}/assets/efui-table.js", ()
            => Results.Text(EfUiTableAssets.ScriptContent, "text/javascript")), options);

        RequireBrowserAuthorization(app.MapGet(options.RoutePrefix, (IServiceProvider services)
            => RenderIndex(options, services)), options);

        RequireBrowserAuthorization(app.MapGet($"{options.RoutePrefix}/{{entity}}", (string entity, HttpRequest request, IServiceProvider services)
            => RenderEntityList(options, entity, request, services)), options);

        RequireBrowserAuthorization(app.MapGet($"{options.RoutePrefix}/{{entity}}/data", (string entity, HttpRequest request, IServiceProvider services)
            => RenderEntityListData(options, entity, request, services)), options);

        RequireBrowserAuthorization(app.MapGet($"{options.RoutePrefix}/{{entity}}/new", (string entity, HttpContext httpContext, IServiceProvider services)
            => RenderCreateForm(options, entity, httpContext, services)), options);

        RequireBrowserAuthorization(app.MapGet($"{options.RoutePrefix}/{{entity}}/{{id}}/edit", (string entity, string id, HttpContext httpContext, IServiceProvider services)
            => RenderEditFormAsync(options, entity, id, httpContext, services)), options);

        RequireEditAuthorization(app.MapPost($"{options.RoutePrefix}/{{entity}}", (string entity, HttpRequest request, IServiceProvider services)
            => CreateEntityAsync(options, entity, request, services)), options);

        RequireEditAuthorization(app.MapPost($"{options.RoutePrefix}/{{entity}}/{{id}}", (string entity, string id, HttpRequest request, IServiceProvider services)
            => UpdateEntityAsync(options, entity, id, request, services)), options);

        RequireEditAuthorization(app.MapPost($"{options.RoutePrefix}/{{entity}}/{{id}}/delete", (string entity, string id, IServiceProvider services)
            => DeleteEntityAsync(options, entity, id, services)), options);
    }

    private static IResult RenderIndex(EfUiOptions options, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var discovery = DiscoverEntities(dbContext);
        var html = new HtmlPageRenderer().RenderIndex(
            options.RoutePrefix,
            discovery.Entities,
            GetRenderableIssueMessages(discovery),
            GetBlockingIssueMessages(discovery));
        return Results.Content(html, HtmlContentType);
    }

    private static IResult RenderEntityList(EfUiOptions options, string entity, HttpRequest request, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var discovery = DiscoverEntities(dbContext);
        var metadata = GetEntityMetadata(discovery, entity);
        if (metadata is null)
        {
            return RenderMissingEntityResult(options.RoutePrefix, discovery, entity);
        }

        var view = BuildRenderedListView(options.RoutePrefix, dbContext, metadata, request, GetRenderableIssueMessages(discovery, entity));
        var html = new HtmlPageRenderer().RenderList(options.RoutePrefix, metadata, view, CanMutate(options, request.HttpContext.User));
        return Results.Content(html, HtmlContentType);
    }

    private static IResult RenderEntityListData(EfUiOptions options, string entity, HttpRequest request, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var discovery = DiscoverEntities(dbContext);
        var metadata = GetEntityMetadata(discovery, entity);
        if (metadata is null)
        {
            return Results.NotFound();
        }

        var view = BuildRenderedListView(options.RoutePrefix, dbContext, metadata, request, GetRenderableIssueMessages(discovery, entity));
        return Results.Text(JsonSerializer.Serialize(RenderedListPayloadFactory.Create(options.RoutePrefix, metadata, view, CanMutate(options, request.HttpContext.User))), "application/json");
    }

    private static IResult RenderCreateForm(EfUiOptions options, string entity, HttpContext httpContext, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var discovery = DiscoverEntities(dbContext);
        var metadata = GetEntityMetadata(discovery, entity);
        if (metadata is null)
        {
            return RenderMissingEntityResult(options.RoutePrefix, discovery, entity);
        }

        var html = new HtmlPageRenderer().RenderEditForm(
            options.RoutePrefix,
            metadata,
            null,
            true,
            new Dictionary<string, string[]>(),
            null,
            fieldOptions: BuildFieldOptions(dbContext, metadata, null, null));
        return Results.Content(html, HtmlContentType);
    }

    private static async Task<IResult> RenderEditFormAsync(EfUiOptions options, string entity, string id, HttpContext httpContext, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var discovery = DiscoverEntities(dbContext);
        var metadata = GetEntityMetadata(discovery, entity);
        if (metadata is null)
        {
            return RenderMissingEntityResult(options.RoutePrefix, discovery, entity);
        }

        var key = TryReadKey(dbContext, metadata, id);
        if (key is null)
        {
            return Results.NotFound();
        }

        var model = await dbContext.FindAsync(metadata.ClrType, key);
        if (model is null)
        {
            return Results.NotFound();
        }

        await LoadEditableCollectionsAsync(dbContext, metadata, model, isCreate: false);

        var html = new HtmlPageRenderer().RenderEditForm(
            options.RoutePrefix,
            metadata,
            model,
            false,
            new Dictionary<string, string[]>(),
            key,
            fieldOptions: BuildFieldOptions(dbContext, metadata, model, null));
        return Results.Content(html, HtmlContentType);
    }

    private static async Task<IResult> CreateEntityAsync(EfUiOptions options, string entity, HttpRequest request, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var discovery = DiscoverEntities(dbContext);
        var metadata = GetEntityMetadata(discovery, entity);
        if (metadata is null)
        {
            return RenderMissingEntityResult(options.RoutePrefix, discovery, entity);
        }

        var values = EnsureCollectionFieldsPresent(metadata, await ReadFormAsync(request), isCreate: true);
        var result = await CreateCrudService().CreateAsync(dbContext, entity, values);

        return result.IsSuccess
            ? Results.Redirect($"{options.RoutePrefix}/{entity}")
            : CreateFailureResult(options.RoutePrefix, dbContext, entity, result, null, isCreate: true, submittedValues: values);
    }

    private static async Task<IResult> UpdateEntityAsync(EfUiOptions options, string entity, string id, HttpRequest request, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var discovery = DiscoverEntities(dbContext);
        var metadata = GetEntityMetadata(discovery, entity);
        if (metadata is null)
        {
            return RenderMissingEntityResult(options.RoutePrefix, discovery, entity);
        }

        var key = TryReadKey(dbContext, metadata, id);
        if (key is null)
        {
            return Results.NotFound();
        }

        var values = EnsureCollectionFieldsPresent(metadata, await ReadFormAsync(request), isCreate: false);
        var result = await CreateCrudService().UpdateAsync(dbContext, entity, key, values);

        return result.IsSuccess
            ? Results.Redirect($"{options.RoutePrefix}/{entity}")
            : CreateFailureResult(options.RoutePrefix, dbContext, entity, result, key, isCreate: false, submittedValues: values);
    }

    private static async Task<IResult> DeleteEntityAsync(EfUiOptions options, string entity, string id, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var discovery = DiscoverEntities(dbContext);
        var metadata = GetEntityMetadata(discovery, entity);
        if (metadata is null)
        {
            return RenderMissingEntityResult(options.RoutePrefix, discovery, entity);
        }

        var key = TryReadKey(dbContext, metadata, id);
        if (key is null)
        {
            return Results.NotFound();
        }

        var result = await CreateCrudService().DeleteAsync(dbContext, entity, key);
        if (!result.IsSuccess)
        {
            if (result.Errors.TryGetValue("id", out var idErrors) && idErrors.Contains("Row not found."))
            {
                return Results.NotFound();
            }

            return Results.BadRequest(result.Errors);
        }

        var rows = ReadRows(dbContext, metadata.ClrType);
        var relatedValueLookups = BuildRelatedValueLookups(dbContext, metadata);
        var html = new HtmlPageRenderer().RenderList(
            options.RoutePrefix,
            metadata,
            new RenderedListView(CreateRenderedListRows(options.RoutePrefix, metadata, rows, relatedValueLookups)));
        return Results.Content(html, HtmlContentType);
    }

    private static void RequireBrowserAuthorization(RouteHandlerBuilder builder, EfUiOptions options)
    {
        if (!options.RequireAuthorization)
        {
            return;
        }

        builder.RequireAuthorization(new AuthorizeAttribute
        {
            Roles = string.Join(',', new[] { options.ReadOnlyRoleName, options.EditRoleName }.Where(role => !string.IsNullOrWhiteSpace(role)))
        });
    }

    private static void RequireEditAuthorization(RouteHandlerBuilder builder, EfUiOptions options)
    {
        if (!options.RequireAuthorization)
        {
            return;
        }

        builder.RequireAuthorization(new AuthorizeAttribute
        {
            Roles = options.EditRoleName
        });
    }

    private static bool CanMutate(EfUiOptions options, ClaimsPrincipal user)
        => !options.RequireAuthorization || user.IsInRole(options.EditRoleName);

    private static DbContext ResolveDbContext(IServiceProvider services, Type dbContextType)
        => (DbContext)services.GetRequiredService(dbContextType);

    private static EntityCrudService CreateCrudService()
        => new(new EfEntityMetadataProvider(), new ScalarValueBinder());

    private static EfEntityMetadataProvider CreateMetadataProvider()
        => new();

    private static EntityDiscoveryResult DiscoverEntities(DbContext dbContext)
        => CreateMetadataProvider().GetDiscoveryResult(dbContext);

    private static EntityMetadata? GetEntityMetadata(EntityDiscoveryResult discovery, string entity)
        => discovery.Entities.SingleOrDefault(x => x.RouteName == entity);

    private static IReadOnlyList<string> GetRenderableIssueMessages(EntityDiscoveryResult discovery)
        => discovery.Issues
            .Where(issue => issue.CanRender)
            .Select(issue => $"{issue.RouteName} — {issue.Message}")
            .ToList();

    private static IReadOnlyList<string> GetRenderableIssueMessages(EntityDiscoveryResult discovery, string entity)
        => discovery.Issues
            .Where(issue => issue.CanRender && string.Equals(issue.RouteName, entity, StringComparison.OrdinalIgnoreCase))
            .Select(issue => issue.Message)
            .ToList();

    private static IReadOnlyList<string> GetBlockingIssueMessages(EntityDiscoveryResult discovery)
        => discovery.Issues
            .Where(issue => !issue.CanRender)
            .Select(issue => $"{issue.RouteName} — {issue.Message}")
            .ToList();

    private static IResult RenderMissingEntityResult(string routePrefix, EntityDiscoveryResult discovery, string entity)
    {
        var blockingIssues = discovery.Issues
            .Where(issue => !issue.CanRender && string.Equals(issue.RouteName, entity, StringComparison.OrdinalIgnoreCase))
            .Select(issue => issue.Message)
            .ToList();

        return blockingIssues.Count > 0
            ? Results.Content(HtmlPageRenderer.RenderErrorPage(routePrefix, entity, blockingIssues), HtmlContentType, statusCode: StatusCodes.Status400BadRequest)
            : Results.NotFound();
    }

    private static BoundTableQuery BindTableQuery(HttpRequest request, EntityMetadata metadata)
    {
        var fields = metadata.AllProperties
            .Select(property => new TableQueryField(property.Name, IsFilterable: true, IsSortable: true))
            .ToDictionary(field => field.Name, StringComparer.Ordinal);
        var errors = new List<string>();
        var filters = ParseFilterClauses(request.Query, fields, errors).ToList();
        var sorts = ParseSortClauses(request.Query, fields, errors).ToList();

        return new BoundTableQuery(
            new TableQuery(filters, sorts, ReadNonNegativeInt(request, "offset", 0, errors), ReadPositiveInt(request, "limit", 50, errors)),
            errors);
    }

    private static IEnumerable<TableFilterClause> ParseFilterClauses(IQueryCollection query, IReadOnlyDictionary<string, TableQueryField> fields, ICollection<string> errors)
    {
        foreach (var index in GetClauseIndexes(query, "filter"))
        {
            if (TryParseFilterClause(query, index, fields, errors, out var filter))
            {
                yield return filter;
            }
        }
    }

    private static bool TryParseFilterClause(IQueryCollection query, int index, IReadOnlyDictionary<string, TableQueryField> fields, ICollection<string> errors, out TableFilterClause filter)
    {
        var field = query[$"filter.{index}.field"].FirstOrDefault();
        var op = query[$"filter.{index}.op"].FirstOrDefault();
        var value = query[$"filter.{index}.value"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(field) && string.IsNullOrWhiteSpace(op) && string.IsNullOrWhiteSpace(value))
        {
            filter = default!;
            return false;
        }

        if (string.IsNullOrWhiteSpace(field) || !fields.TryGetValue(field, out var fieldDefinition) || !fieldDefinition.IsFilterable)
        {
            errors.Add($"Unsupported filter field '{field}'.");
            filter = default!;
            return false;
        }

        if (string.IsNullOrWhiteSpace(op) || !fieldDefinition.SupportedOperators.Contains(op, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"Unsupported filter operator '{op}' for field '{field}'.");
            filter = default!;
            return false;
        }

        filter = new TableFilterClause(field, op, value);
        return true;
    }

    private static IEnumerable<TableSortClause> ParseSortClauses(IQueryCollection query, IReadOnlyDictionary<string, TableQueryField> fields, ICollection<string> errors)
    {
        foreach (var index in GetClauseIndexes(query, "sort"))
        {
            if (TryParseSortClause(query, index, fields, errors, out var sort))
            {
                yield return sort;
            }
        }
    }

    private static bool TryParseSortClause(IQueryCollection query, int index, IReadOnlyDictionary<string, TableQueryField> fields, ICollection<string> errors, out TableSortClause sort)
    {
        var field = query[$"sort.{index}.field"].FirstOrDefault();
        var direction = query[$"sort.{index}.dir"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(field) && string.IsNullOrWhiteSpace(direction))
        {
            sort = default!;
            return false;
        }

        if (string.IsNullOrWhiteSpace(field) || !fields.TryGetValue(field, out var fieldDefinition) || !fieldDefinition.IsSortable)
        {
            errors.Add($"Unsupported sort field '{field}'.");
            sort = default!;
            return false;
        }

        if (!string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Unsupported sort direction '{direction}'.");
            sort = default!;
            return false;
        }

        sort = new TableSortClause(field, direction!);
        return true;
    }

    private static IEnumerable<int> GetClauseIndexes(IQueryCollection query, string prefix)
        => query.Keys
            .Where(key => key.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase))
            .Select(key => key.Split('.', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length >= 3)
            .Select(parts => int.TryParse(parts[1], out var index) ? index : -1)
            .Where(index => index >= 0)
            .Distinct()
            .OrderBy(index => index);

    private static int ReadNonNegativeInt(HttpRequest request, string key, int fallback, ICollection<string> errors)
    {
        var rawValue = request.Query[key].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return fallback;
        }

        if (int.TryParse(rawValue, out var parsed) && parsed >= 0)
        {
            return parsed;
        }

        errors.Add($"Unsupported {key} value '{rawValue}'.");
        return fallback;
    }

    private static int ReadPositiveInt(HttpRequest request, string key, int fallback, ICollection<string> errors)
    {
        var rawValue = request.Query[key].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return fallback;
        }

        if (int.TryParse(rawValue, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        errors.Add($"Unsupported {key} value '{rawValue}'.");
        return fallback;
    }

    private sealed record BoundTableQuery(TableQuery Query, IReadOnlyList<string> Errors);

    private static RenderedListView BuildRenderedListView(string routePrefix, DbContext dbContext, EntityMetadata metadata, HttpRequest request, IReadOnlyList<string>? warnings = null)
    {
        var queryResult = BindTableQuery(request, metadata);
        var relatedValueLookups = BuildRelatedValueLookups(dbContext, metadata);
        var rows = ApplyTableQuery(ReadRows(dbContext, metadata.ClrType), metadata, queryResult.Query, relatedValueLookups);
        return new RenderedListView(
            CreateRenderedListRows(routePrefix, metadata, rows, relatedValueLookups),
            queryResult.Query.Filters.Select(filter => new RenderedListFilter(filter.Field, filter.Operator, filter.Value)).ToList(),
            queryResult.Query.Sorts.Select(sort => new RenderedListSort(sort.Field, sort.Direction)).ToList(),
            queryResult.Errors,
            queryResult.Query.Offset,
            queryResult.Query.Limit,
            warnings);
    }

    private static IReadOnlyList<object> ReadRows(DbContext dbContext, Type entityClrType)
    {
        var setMethod = typeof(DbContext).GetMethods()
            .Single(method => method.Name == nameof(DbContext.Set)
                              && method.IsGenericMethodDefinition
                              && method.GetParameters().Length == 0);

        var queryable = (System.Collections.IEnumerable)setMethod.MakeGenericMethod(entityClrType).Invoke(dbContext, null)!;
        return queryable.Cast<object>().ToList();
    }

    private static IReadOnlyList<RenderedListRow> CreateRenderedListRows(string routePrefix, EntityMetadata metadata, IReadOnlyList<object> rows, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> relatedValueLookups)
    {
        return rows.Select(row => new RenderedListRow(
            FormatValue(row.GetType().GetProperty(metadata.PrimaryKeyProperty.Name)?.GetValue(row)),
            metadata.AllProperties.ToDictionary(
                property => property.Name,
                property => CreateRenderedListCell(routePrefix, row, property, relatedValueLookups)))).ToList();
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> BuildRelatedValueLookups(DbContext dbContext, EntityMetadata metadata)
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
            lookups[foreignKeyProperty.Name] = ReadRows(dbContext, foreignKey.PrincipalEntityType.ClrType)
                .ToDictionary(
                    row => FormatValue(row.GetType().GetProperty(relatedPrimaryKey.Name)?.GetValue(row)),
                    row => GetRelatedEntityLabel(row, relatedPrimaryKey.Name, relatedProperty.RelatedDisplayPropertyName),
                    StringComparer.Ordinal);
        }

        return lookups;
    }

    private static IReadOnlyList<object> ApplyTableQuery(IReadOnlyList<object> rows, EntityMetadata metadata, TableQuery query, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> relatedValueLookups)
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

        return (orderedRows ?? filteredRows)
            .Skip(query.Offset)
            .Take(query.Limit)
            .ToList();
    }

    private static bool MatchesFilter(object row, EntityPropertyMetadata property, TableFilterClause filter, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> relatedValueLookups)
    {
        var candidate = GetQueryDisplayValue(row, property, relatedValueLookups);
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

    private static string GetQueryDisplayValue(object row, EntityPropertyMetadata property, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> relatedValueLookups)
        => GetRenderedListCellValue(row, property.Name, relatedValueLookups);

    private static object? GetSortKeyValue(object row, EntityPropertyMetadata property, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> relatedValueLookups)
        => relatedValueLookups.ContainsKey(property.Name)
            ? GetQueryDisplayValue(row, property, relatedValueLookups)
            : row.GetType().GetProperty(property.Name)?.GetValue(row);

    private static RenderedListCell CreateRenderedListCell(string routePrefix, object row, EntityPropertyMetadata property, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> relatedValueLookups)
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

    private static string GetRenderedListCellValue(object row, string propertyName, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> relatedValueLookups)
    {
        var rawValue = row.GetType().GetProperty(propertyName)?.GetValue(row);
        var formattedValue = FormatValue(rawValue);

        return relatedValueLookups.TryGetValue(propertyName, out var lookup)
               && lookup.TryGetValue(formattedValue, out var label)
            ? label
            : formattedValue;
    }

    private static object? TryReadKey(DbContext dbContext, EntityMetadata metadata, string id)
    {
        if (dbContext.Model.FindEntityType(metadata.ClrType)?.FindPrimaryKey() is null)
        {
            return null;
        }

        var bindResult = new ScalarValueBinder().Bind(metadata.PrimaryKeyProperty.ClrType, id);
        return bindResult.IsSuccess ? bindResult.Value : null;
    }

    private static IResult CreateFailureResult(string routePrefix, DbContext dbContext, string entity, CrudOperationResult result, object? key, bool isCreate, IReadOnlyDictionary<string, string[]> submittedValues)
    {
        if (result.Errors.ContainsKey("entity") || result.Errors.ContainsKey("id"))
        {
            return Results.NotFound();
        }

        var discovery = DiscoverEntities(dbContext);
        var metadata = GetEntityMetadata(discovery, entity);
        if (metadata is null)
        {
            return RenderMissingEntityResult(routePrefix, discovery, entity);
        }

        var model = !isCreate && key is not null ? dbContext.Find(metadata.ClrType, key) : null;
        if (model is not null)
        {
            LoadEditableCollectionsAsync(dbContext, metadata, model, isCreate).GetAwaiter().GetResult();
        }

        var html = new HtmlPageRenderer().RenderEditForm(
            routePrefix,
            metadata,
            model,
            isCreate,
            result.Errors,
            key,
            submittedValues,
            BuildFieldOptions(dbContext, metadata, model, submittedValues));
        return Results.Content(html, HtmlContentType, statusCode: StatusCodes.Status400BadRequest);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<RelatedEntityOption>> BuildFieldOptions(DbContext dbContext, EntityMetadata metadata, object? model, IReadOnlyDictionary<string, string[]>? submittedValues)
    {
        var options = new Dictionary<string, IReadOnlyList<RelatedEntityOption>>(StringComparer.OrdinalIgnoreCase);
        var fields = metadata.CreateEditableFields.Concat(metadata.UpdateEditableFields).DistinctBy(field => field.Name).ToList();
        var oneToManyFields = fields.Where(field => field.Kind == EditableFieldKind.Collection && field.CollectionRelationshipKind == CollectionRelationshipKind.OneToMany && field.RelatedClrType is not null).ToList();
        var ownerLabels = oneToManyFields.Count == 0
            ? null
            : ReadRows(dbContext, metadata.ClrType)
                .ToDictionary(
                    row => FormatValue(row.GetType().GetProperty(metadata.PrimaryKeyProperty.Name)?.GetValue(row)),
                    row => GetRelatedEntityLabel(row, metadata.PrimaryKeyProperty.Name),
                    StringComparer.Ordinal);

        foreach (var field in fields)
        {
            if (field.Kind is not EditableFieldKind.Reference and not EditableFieldKind.Collection || field.RelatedClrType is null)
            {
                continue;
            }

            var selectedValues = GetSelectedValues(dbContext, field, model, submittedValues);
            options[field.Name] = ReadRows(dbContext, field.RelatedClrType)
                .Select(row => CreateRelatedEntityOption(dbContext, metadata, field, row, selectedValues, model, ownerLabels))
                .ToList();
        }

        return options;
    }

    private static RelatedEntityOption CreateRelatedEntityOption(DbContext dbContext, EntityMetadata metadata, EditableFieldMetadata field, object row, HashSet<string> selectedValues, object? model, IReadOnlyDictionary<string, string>? ownerLabels)
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

    private static string GetRelatedEntityLabel(object row, string primaryKeyPropertyName, string? displayPropertyName = null)
        => EntityDisplayLabelResolver.Resolve(row, displayPropertyName, primaryKeyPropertyName);

    private static HashSet<string> GetSelectedValues(DbContext dbContext, EditableFieldMetadata field, object? model, IReadOnlyDictionary<string, string[]>? submittedValues)
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

    private static string FormatValue(object? value)
        => value switch
        {
            null => string.Empty,
            DateTime dateTime => dateTime.ToString("O"),
            _ => value.ToString() ?? string.Empty
        };

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

    private static IReadOnlyDictionary<string, string[]> EnsureCollectionFieldsPresent(EntityMetadata metadata, Dictionary<string, string[]> submittedValues, bool isCreate)
    {
        var editableFields = isCreate ? metadata.CreateEditableFields : metadata.UpdateEditableFields;
        foreach (var field in editableFields.Where(field => field.Kind == EditableFieldKind.Collection && !submittedValues.ContainsKey(field.Name)))
        {
            submittedValues[field.Name] = [];
        }

        return submittedValues;
    }

    private static async Task<Dictionary<string, string[]>> ReadFormAsync(HttpRequest request)
    {
        var form = await request.ReadFormAsync();
        return form.ToDictionary(
            x => x.Key,
            x => x.Value.Select(value => value ?? string.Empty).ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }
}
