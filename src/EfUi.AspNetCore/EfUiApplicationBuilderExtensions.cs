using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using EfUi.Core.Binding;
using EfUi.Core.Crud;
using EfUi.Core.Metadata;
using EfUi.Core.Query;
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

        RequireBrowserAuthorization(app.MapGet($"{options.RoutePrefix}/assets/tabulator.min.css", ()
            => Results.Text(EfUiTabulatorAssets.StylesheetContent, "text/css")), options);

        RequireBrowserAuthorization(app.MapGet($"{options.RoutePrefix}/assets/tabulator.min.js", ()
            => Results.Text(EfUiTabulatorAssets.ScriptContent, "text/javascript")), options);

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

        RequireEditAuthorization(app.MapPost($"{options.RoutePrefix}/{{entity}}/{{id}}/delete", (string entity, string id, HttpRequest request, IServiceProvider services)
            => DeleteEntityAsync(options, entity, id, request, services)), options);
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

    private static async Task<IResult> RenderEntityList(EfUiOptions options, string entity, HttpRequest request, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var discovery = DiscoverEntities(dbContext);
        var metadata = GetEntityMetadata(discovery, entity);
        if (metadata is null)
        {
            return RenderMissingEntityResult(options.RoutePrefix, discovery, entity);
        }

        var view = await BuildRenderedListView(
            options.RoutePrefix,
            dbContext,
            metadata,
            request,
            GetRenderableIssueMessages(discovery, entity));
        var canMutate = CanMutate(options, request.HttpContext.User);
        var antiForgeryToken = canMutate ? EfUiRequestForgery.GetOrCreateRequestToken(request.HttpContext, options.RoutePrefix, options.AntiforgeryKeyDirectory) : null;
        var html = new HtmlPageRenderer().RenderList(options.RoutePrefix, metadata, view, canMutate, antiForgeryToken);
        return Results.Content(html, HtmlContentType);
    }

    private static async Task<IResult> RenderEntityListData(EfUiOptions options, string entity, HttpRequest request, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var discovery = DiscoverEntities(dbContext);
        var metadata = GetEntityMetadata(discovery, entity);
        if (metadata is null)
        {
            return Results.NotFound();
        }

        var view = await BuildRenderedListView(
            options.RoutePrefix,
            dbContext,
            metadata,
            request,
            GetRenderableIssueMessages(discovery, entity));
        var canMutate = CanMutate(options, request.HttpContext.User);
        var antiForgeryToken = canMutate ? EfUiRequestForgery.GetOrCreateRequestToken(request.HttpContext, options.RoutePrefix, options.AntiforgeryKeyDirectory) : null;
        return Results.Text(JsonSerializer.Serialize(RenderedListPayloadFactory.Create(options.RoutePrefix, metadata, view, canMutate, antiForgeryToken)), "application/json");
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

        var rowCache = new RequestRowCache();
        var antiForgeryToken = EfUiRequestForgery.GetOrCreateRequestToken(httpContext, options.RoutePrefix, options.AntiforgeryKeyDirectory);
        var html = new HtmlPageRenderer().RenderEditForm(
            options.RoutePrefix,
            metadata,
            null,
            true,
            new Dictionary<string, string[]>(),
            null,
            fieldOptions: BuildFieldOptions(dbContext, metadata, null, null, rowCache, isCreate: true),
            antiForgeryToken: antiForgeryToken);
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

        var rowCache = new RequestRowCache();
        var antiForgeryToken = EfUiRequestForgery.GetOrCreateRequestToken(httpContext, options.RoutePrefix, options.AntiforgeryKeyDirectory);
        var html = new HtmlPageRenderer().RenderEditForm(
            options.RoutePrefix,
            metadata,
            model,
            false,
            new Dictionary<string, string[]>(),
            key,
            fieldOptions: BuildFieldOptions(dbContext, metadata, model, null, rowCache, isCreate: false),
            antiForgeryToken: antiForgeryToken);
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

        var rowCache = new RequestRowCache();
        var values = EnsureCollectionFieldsPresent(metadata, await ReadFormAsync(request), isCreate: true);
        if (!EfUiRequestForgery.ValidateRequest(values, request.HttpContext, options.RoutePrefix, options.AntiforgeryKeyDirectory))
        {
            return Results.BadRequest();
        }

        var result = await CreateCrudService().CreateAsync(dbContext, entity, values);

        return result.IsSuccess
            ? Results.Redirect($"{options.RoutePrefix}/{entity}")
            : CreateFailureResult(options.RoutePrefix, request.HttpContext, options.AntiforgeryKeyDirectory, dbContext, entity, result, null, rowCache, isCreate: true, submittedValues: values);
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

        var rowCache = new RequestRowCache();
        var values = EnsureCollectionFieldsPresent(metadata, await ReadFormAsync(request), isCreate: false);
        if (!EfUiRequestForgery.ValidateRequest(values, request.HttpContext, options.RoutePrefix, options.AntiforgeryKeyDirectory))
        {
            return Results.BadRequest();
        }

        var result = await CreateCrudService().UpdateAsync(dbContext, entity, key, values);

        return result.IsSuccess
            ? Results.Redirect($"{options.RoutePrefix}/{entity}")
            : CreateFailureResult(options.RoutePrefix, request.HttpContext, options.AntiforgeryKeyDirectory, dbContext, entity, result, key, rowCache, isCreate: false, submittedValues: values);
    }

    private static async Task<IResult> DeleteEntityAsync(EfUiOptions options, string entity, string id, HttpRequest request, IServiceProvider services)
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

        var values = await ReadFormAsync(request);
        if (!EfUiRequestForgery.ValidateRequest(values, request.HttpContext, options.RoutePrefix, options.AntiforgeryKeyDirectory))
        {
            return Results.BadRequest();
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

        var view = await BuildRenderedListView(
            options.RoutePrefix,
            dbContext,
            metadata,
            request,
            GetRenderableIssueMessages(discovery, entity));
        var canMutate = CanMutate(options, request.HttpContext.User);
        var antiForgeryToken = canMutate ? EfUiRequestForgery.GetOrCreateRequestToken(request.HttpContext, options.RoutePrefix, options.AntiforgeryKeyDirectory) : null;
        var html = new HtmlPageRenderer().RenderList(
            options.RoutePrefix,
            metadata,
            view,
            canMutate,
            antiForgeryToken);
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
        => discovery.Entities.SingleOrDefault(x => string.Equals(x.RouteName, entity, StringComparison.OrdinalIgnoreCase));

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

    private static readonly ConcurrentDictionary<Type, Func<DbContext, IReadOnlyList<object>>> ReadRowsAccessors = new();

    private static async Task<RenderedListView> BuildRenderedListView(
        string routePrefix,
        DbContext dbContext,
        EntityMetadata metadata,
        HttpRequest request,
        IReadOnlyList<string>? warnings = null)
    {
        var parsed = TableQueryRequestParser.Parse(request);
        var result = await new EntityListQueryExecutor().ExecuteAsync(
            dbContext,
            metadata,
            parsed.Query,
            request.HttpContext.RequestAborted);
        return RenderedListViewAdapter.Create(routePrefix, metadata, result, parsed.Errors, warnings);
    }

    private static IReadOnlyList<object> ReadRows(DbContext dbContext, Type entityClrType)
        => ReadRowsAccessors.GetOrAdd(entityClrType, CreateReadRowsAccessor)(dbContext);

    private static Func<DbContext, IReadOnlyList<object>> CreateReadRowsAccessor(Type entityClrType)
    {
        var method = typeof(EfUiApplicationBuilderExtensions).GetMethod(nameof(ReadRowsCore), BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Could not resolve the row reader method.");

        return (Func<DbContext, IReadOnlyList<object>>)method.MakeGenericMethod(entityClrType).CreateDelegate(typeof(Func<DbContext, IReadOnlyList<object>>));
    }

    private static IReadOnlyList<object> ReadRowsCore<TEntity>(DbContext dbContext)
        where TEntity : class
        => dbContext.Set<TEntity>().Cast<object>().ToList();

    private static object? TryReadKey(DbContext dbContext, EntityMetadata metadata, string id)
    {
        if (dbContext.Model.FindEntityType(metadata.ClrType)?.FindPrimaryKey() is null)
        {
            return null;
        }

        var bindResult = new ScalarValueBinder().Bind(metadata.PrimaryKeyProperty.ClrType, id);
        return bindResult.IsSuccess ? bindResult.Value : null;
    }

    private static IResult CreateFailureResult(string routePrefix, HttpContext httpContext, string? antiforgeryKeyDirectory, DbContext dbContext, string entity, CrudOperationResult result, object? key, RequestRowCache rowCache, bool isCreate, IReadOnlyDictionary<string, string[]> submittedValues)
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

        var antiForgeryToken = EfUiRequestForgery.GetOrCreateRequestToken(httpContext, routePrefix, antiforgeryKeyDirectory);
        var html = new HtmlPageRenderer().RenderEditForm(
            routePrefix,
            metadata,
            model,
            isCreate,
            result.Errors,
            key,
            submittedValues,
            BuildFieldOptions(dbContext, metadata, model, submittedValues, rowCache, isCreate),
            antiForgeryToken: antiForgeryToken);
        return Results.Content(html, HtmlContentType, statusCode: StatusCodes.Status400BadRequest);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<RelatedEntityOption>> BuildFieldOptions(DbContext dbContext, EntityMetadata metadata, object? model, IReadOnlyDictionary<string, string[]>? submittedValues, RequestRowCache rowCache, bool isCreate)
    {
        var options = new Dictionary<string, IReadOnlyList<RelatedEntityOption>>(StringComparer.OrdinalIgnoreCase);
        var fields = isCreate ? metadata.CreateEditableFields : metadata.UpdateEditableFields;
        var oneToManyFields = fields.Where(field => field.Kind == EditableFieldKind.Collection && field.CollectionRelationshipKind == CollectionRelationshipKind.OneToMany && field.RelatedClrType is not null).ToList();
        var ownerLabels = oneToManyFields.Count == 0
            ? null
            : rowCache.GetRows(dbContext, metadata.ClrType)
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
            var relatedRows = GetRelatedRows(dbContext, field.RelatedClrType, rowCache);
            options[field.Name] = relatedRows
                .Select(row => CreateRelatedEntityOption(dbContext, metadata, field, row, selectedValues, model, ownerLabels))
                .ToList();
        }

        return options;
    }

    private static IReadOnlyList<object> GetRelatedRows(DbContext dbContext, Type relatedClrType, RequestRowCache rowCache)
        => rowCache.GetRows(dbContext, relatedClrType);

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
