using EfUi.Core.Binding;
using EfUi.Core.Crud;
using EfUi.Core.Metadata;
using EfUi.Core.Rendering;
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
        app.MapGet(options.RoutePrefix, (IServiceProvider services)
            => RenderIndex(options, services));

        app.MapGet($"{options.RoutePrefix}/{{entity}}", (string entity, IServiceProvider services)
            => RenderEntityList(options, entity, services));

        app.MapGet($"{options.RoutePrefix}/{{entity}}/new", (string entity, IServiceProvider services)
            => RenderCreateForm(options, entity, services));

        app.MapGet($"{options.RoutePrefix}/{{entity}}/{{id}}/edit", (string entity, string id, IServiceProvider services)
            => RenderEditFormAsync(options, entity, id, services));

        app.MapPost($"{options.RoutePrefix}/{{entity}}", (string entity, HttpRequest request, IServiceProvider services)
            => CreateEntityAsync(options, entity, request, services));

        app.MapPost($"{options.RoutePrefix}/{{entity}}/{{id}}", (string entity, string id, HttpRequest request, IServiceProvider services)
            => UpdateEntityAsync(options, entity, id, request, services));

        app.MapPost($"{options.RoutePrefix}/{{entity}}/{{id}}/delete", (string entity, string id, IServiceProvider services)
            => DeleteEntityAsync(options, entity, id, services));
    }

    private static IResult RenderIndex(EfUiOptions options, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var entities = new EfEntityMetadataProvider().GetEntities(dbContext);
        var html = new HtmlPageRenderer().RenderIndex(options.RoutePrefix, entities);
        return Results.Content(html, HtmlContentType);
    }

    private static IResult RenderEntityList(EfUiOptions options, string entity, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var metadata = GetEntityMetadata(dbContext, entity);
        if (metadata is null)
        {
            return Results.NotFound();
        }

        var html = new HtmlPageRenderer().RenderList(options.RoutePrefix, metadata, ReadRows(dbContext, metadata.ClrType));
        return Results.Content(html, HtmlContentType);
    }

    private static IResult RenderCreateForm(EfUiOptions options, string entity, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var metadata = GetEntityMetadata(dbContext, entity);
        if (metadata is null)
        {
            return Results.NotFound();
        }

        var html = new HtmlPageRenderer().RenderEditForm(options.RoutePrefix, metadata, null, true, new Dictionary<string, string[]>(), null);
        return Results.Content(html, HtmlContentType);
    }

    private static async Task<IResult> RenderEditFormAsync(EfUiOptions options, string entity, string id, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var metadata = GetEntityMetadata(dbContext, entity);
        if (metadata is null)
        {
            return Results.NotFound();
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

        var html = new HtmlPageRenderer().RenderEditForm(options.RoutePrefix, metadata, model, false, new Dictionary<string, string[]>(), key);
        return Results.Content(html, HtmlContentType);
    }

    private static async Task<IResult> CreateEntityAsync(EfUiOptions options, string entity, HttpRequest request, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var values = await ReadFormAsync(request);
        var result = await CreateCrudService().CreateAsync(dbContext, entity, values);

        return result.IsSuccess
            ? Results.Redirect($"{options.RoutePrefix}/{entity}")
            : CreateFailureResult(options.RoutePrefix, dbContext, entity, result, null, isCreate: true, submittedValues: values);
    }

    private static async Task<IResult> UpdateEntityAsync(EfUiOptions options, string entity, string id, HttpRequest request, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var metadata = GetEntityMetadata(dbContext, entity);
        if (metadata is null)
        {
            return Results.NotFound();
        }

        var key = TryReadKey(dbContext, metadata, id);
        if (key is null)
        {
            return Results.NotFound();
        }

        var values = await ReadFormAsync(request);
        var result = await CreateCrudService().UpdateAsync(dbContext, entity, key, values);

        return result.IsSuccess
            ? Results.Redirect($"{options.RoutePrefix}/{entity}")
            : CreateFailureResult(options.RoutePrefix, dbContext, entity, result, key, isCreate: false, submittedValues: values);
    }

    private static async Task<IResult> DeleteEntityAsync(EfUiOptions options, string entity, string id, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var metadata = GetEntityMetadata(dbContext, entity);
        if (metadata is null)
        {
            return Results.NotFound();
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

        var html = new HtmlPageRenderer().RenderList(options.RoutePrefix, metadata, ReadRows(dbContext, metadata.ClrType));
        return Results.Content(html, HtmlContentType);
    }

    private static DbContext ResolveDbContext(IServiceProvider services, Type dbContextType)
        => (DbContext)services.GetRequiredService(dbContextType);

    private static EntityCrudService CreateCrudService()
        => new(new EfEntityMetadataProvider(), new ScalarValueBinder());

    private static EntityMetadata? GetEntityMetadata(DbContext dbContext, string entity)
        => new EfEntityMetadataProvider().GetEntities(dbContext).SingleOrDefault(x => x.RouteName == entity);

    private static IReadOnlyList<object> ReadRows(DbContext dbContext, Type entityClrType)
    {
        var setMethod = typeof(DbContext).GetMethods()
            .Single(method => method.Name == nameof(DbContext.Set)
                              && method.IsGenericMethodDefinition
                              && method.GetParameters().Length == 0);

        var queryable = (System.Collections.IEnumerable)setMethod.MakeGenericMethod(entityClrType).Invoke(dbContext, null)!;
        return queryable.Cast<object>().ToList();
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

    private static IResult CreateFailureResult(string routePrefix, DbContext dbContext, string entity, CrudOperationResult result, object? key, bool isCreate, IReadOnlyDictionary<string, string?> submittedValues)
    {
        if (result.Errors.ContainsKey("entity") || result.Errors.ContainsKey("id"))
        {
            return Results.NotFound();
        }

        var metadata = GetEntityMetadata(dbContext, entity);
        if (metadata is null)
        {
            return Results.NotFound();
        }

        var model = !isCreate && key is not null ? dbContext.Find(metadata.ClrType, key) : null;
        var html = new HtmlPageRenderer().RenderEditForm(routePrefix, metadata, model, isCreate, result.Errors, key, submittedValues);
        return Results.Content(html, HtmlContentType, statusCode: StatusCodes.Status400BadRequest);
    }

    private static async Task<Dictionary<string, string?>> ReadFormAsync(HttpRequest request)
    {
        var form = await request.ReadFormAsync();
        return form.ToDictionary(x => x.Key, x => (string?)x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
    }
}
