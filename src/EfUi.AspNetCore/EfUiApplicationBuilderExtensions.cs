using System.Security.Claims;
using System.Text.Json;
using EfUi.Core.Crud;
using EfUi.Core.Metadata;
using EfUi.Core.Orchestration;
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

        MapEfUiRoutes(app, options, options.FlowOrchestrator ?? new EfUiFlowOrchestrator());
        return app;
    }

    private static void MapEfUiRoutes(WebApplication app, EfUiOptions options, IEfUiFlowOrchestrator orchestrator)
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
            => RenderIndex(options, orchestrator, services)), options);
        RequireBrowserAuthorization(app.MapGet($"{options.RoutePrefix}/{{entity}}", (string entity, HttpRequest request, IServiceProvider services)
            => RenderEntityList(options, orchestrator, entity, request, services)), options);
        RequireBrowserAuthorization(app.MapGet($"{options.RoutePrefix}/{{entity}}/data", (string entity, HttpRequest request, IServiceProvider services)
            => RenderEntityListData(options, orchestrator, entity, request, services)), options);
        RequireBrowserAuthorization(app.MapGet($"{options.RoutePrefix}/{{entity}}/new", (string entity, HttpContext httpContext, IServiceProvider services)
            => RenderCreateFormAsync(options, orchestrator, entity, httpContext, services)), options);
        RequireBrowserAuthorization(app.MapGet($"{options.RoutePrefix}/{{entity}}/{{id}}/edit", (string entity, string id, HttpContext httpContext, IServiceProvider services)
            => RenderEditFormAsync(options, orchestrator, entity, id, httpContext, services)), options);
        RequireEditAuthorization(app.MapPost($"{options.RoutePrefix}/{{entity}}", (string entity, HttpRequest request, IServiceProvider services)
            => CreateEntityAsync(options, orchestrator, entity, request, services)), options);
        RequireEditAuthorization(app.MapPost($"{options.RoutePrefix}/{{entity}}/{{id}}", (string entity, string id, HttpRequest request, IServiceProvider services)
            => UpdateEntityAsync(options, orchestrator, entity, id, request, services)), options);
        RequireEditAuthorization(app.MapPost($"{options.RoutePrefix}/{{entity}}/{{id}}/delete", (string entity, string id, HttpRequest request, IServiceProvider services)
            => DeleteEntityAsync(options, orchestrator, entity, id, request, services)), options);
    }

    private static IResult RenderIndex(EfUiOptions options, IEfUiFlowOrchestrator orchestrator, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var discovery = orchestrator.GetDiscoveryResult(dbContext);
        return Results.Content(orchestrator.RenderIndexPage(options.RoutePrefix, discovery), HtmlContentType);
    }

    private static async Task<IResult> RenderEntityList(EfUiOptions options, IEfUiFlowOrchestrator orchestrator, string entity, HttpRequest request, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var discovery = orchestrator.GetDiscoveryResult(dbContext);
        var metadata = orchestrator.FindEntityMetadata(discovery, entity);
        if (metadata is null)
        {
            return RenderMissingEntityResult(options, orchestrator, discovery, entity);
        }

        var parsed = TableQueryRequestParser.Parse(request);
        var view = await orchestrator.BuildRenderedListViewAsync(
            options.RoutePrefix,
            dbContext,
            metadata,
            parsed.Query,
            parsed.Errors,
            orchestrator.GetRenderableIssueMessages(discovery, entity),
            cancellationToken: request.HttpContext.RequestAborted);
        var canMutate = CanMutate(options, request.HttpContext.User);
        var antiForgeryToken = canMutate
            ? EfUiRequestForgery.GetOrCreateRequestToken(request.HttpContext, options.RoutePrefix, options.AntiforgeryKeyDirectory)
            : null;
        var html = orchestrator.RenderListPage(options.RoutePrefix, metadata, view, canMutate, antiForgeryToken);
        return Results.Content(html, HtmlContentType);
    }

    private static async Task<IResult> RenderEntityListData(EfUiOptions options, IEfUiFlowOrchestrator orchestrator, string entity, HttpRequest request, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var discovery = orchestrator.GetDiscoveryResult(dbContext);
        var metadata = orchestrator.FindEntityMetadata(discovery, entity);
        if (metadata is null)
        {
            return Results.NotFound();
        }

        var parsed = TableQueryRequestParser.Parse(request);
        var view = await orchestrator.BuildRenderedListViewAsync(
            options.RoutePrefix,
            dbContext,
            metadata,
            parsed.Query,
            parsed.Errors,
            orchestrator.GetRenderableIssueMessages(discovery, entity),
            cancellationToken: request.HttpContext.RequestAborted);
        var canMutate = CanMutate(options, request.HttpContext.User);
        var antiForgeryToken = canMutate
            ? EfUiRequestForgery.GetOrCreateRequestToken(request.HttpContext, options.RoutePrefix, options.AntiforgeryKeyDirectory)
            : null;
        var payload = RenderedListPayloadFactory.Create(options.RoutePrefix, metadata, view, canMutate, antiForgeryToken);
        return Results.Text(JsonSerializer.Serialize(payload), "application/json");
    }

    private static async Task<IResult> RenderCreateFormAsync(EfUiOptions options, IEfUiFlowOrchestrator orchestrator, string entity, HttpContext httpContext, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var discovery = orchestrator.GetDiscoveryResult(dbContext);
        var metadata = orchestrator.FindEntityMetadata(discovery, entity);
        if (metadata is null)
        {
            return RenderMissingEntityResult(options, orchestrator, discovery, entity);
        }

        var form = await orchestrator.PrepareFormAsync(dbContext, metadata, null, isCreate: true);
        if (form is null)
        {
            return Results.NotFound();
        }

        var antiForgeryToken = EfUiRequestForgery.GetOrCreateRequestToken(httpContext, options.RoutePrefix, options.AntiforgeryKeyDirectory);
        var html = orchestrator.RenderFormPage(
            options.RoutePrefix,
            metadata,
            form,
            new Dictionary<string, string[]>(),
            null,
            antiForgeryToken);
        return Results.Content(html, HtmlContentType);
    }

    private static async Task<IResult> RenderEditFormAsync(EfUiOptions options, IEfUiFlowOrchestrator orchestrator, string entity, string id, HttpContext httpContext, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var discovery = orchestrator.GetDiscoveryResult(dbContext);
        var metadata = orchestrator.FindEntityMetadata(discovery, entity);
        if (metadata is null)
        {
            return RenderMissingEntityResult(options, orchestrator, discovery, entity);
        }

        var key = orchestrator.TryReadKey(dbContext, metadata, id);
        if (key is null)
        {
            return Results.NotFound();
        }

        var form = await orchestrator.PrepareFormAsync(dbContext, metadata, key, isCreate: false);
        if (form is null)
        {
            return Results.NotFound();
        }

        var antiForgeryToken = EfUiRequestForgery.GetOrCreateRequestToken(httpContext, options.RoutePrefix, options.AntiforgeryKeyDirectory);
        var html = orchestrator.RenderFormPage(
            options.RoutePrefix,
            metadata,
            form,
            new Dictionary<string, string[]>(),
            null,
            antiForgeryToken);
        return Results.Content(html, HtmlContentType);
    }

    private static async Task<IResult> CreateEntityAsync(EfUiOptions options, IEfUiFlowOrchestrator orchestrator, string entity, HttpRequest request, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var discovery = orchestrator.GetDiscoveryResult(dbContext);
        var metadata = orchestrator.FindEntityMetadata(discovery, entity);
        if (metadata is null)
        {
            return RenderMissingEntityResult(options, orchestrator, discovery, entity);
        }

        var values = orchestrator.EnsureCollectionFieldsPresent(metadata, await ReadFormAsync(request), isCreate: true);
        if (!EfUiRequestForgery.ValidateRequest(values, request.HttpContext, options.RoutePrefix, options.AntiforgeryKeyDirectory))
        {
            return Results.BadRequest();
        }

        var result = await orchestrator.CreateAsync(dbContext, entity, values);
        return result.IsSuccess
            ? Results.Redirect($"{options.RoutePrefix}/{entity}")
            : await CreateFailureResultAsync(options, orchestrator, dbContext, discovery, entity, result, null, isCreate: true, values, request.HttpContext);
    }

    private static async Task<IResult> UpdateEntityAsync(EfUiOptions options, IEfUiFlowOrchestrator orchestrator, string entity, string id, HttpRequest request, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var discovery = orchestrator.GetDiscoveryResult(dbContext);
        var metadata = orchestrator.FindEntityMetadata(discovery, entity);
        if (metadata is null)
        {
            return RenderMissingEntityResult(options, orchestrator, discovery, entity);
        }

        var key = orchestrator.TryReadKey(dbContext, metadata, id);
        if (key is null)
        {
            return Results.NotFound();
        }

        var values = orchestrator.EnsureCollectionFieldsPresent(metadata, await ReadFormAsync(request), isCreate: false);
        if (!EfUiRequestForgery.ValidateRequest(values, request.HttpContext, options.RoutePrefix, options.AntiforgeryKeyDirectory))
        {
            return Results.BadRequest();
        }

        var result = await orchestrator.UpdateAsync(dbContext, entity, key, values);
        return result.IsSuccess
            ? Results.Redirect($"{options.RoutePrefix}/{entity}")
            : await CreateFailureResultAsync(options, orchestrator, dbContext, discovery, entity, result, key, isCreate: false, values, request.HttpContext);
    }

    private static async Task<IResult> DeleteEntityAsync(EfUiOptions options, IEfUiFlowOrchestrator orchestrator, string entity, string id, HttpRequest request, IServiceProvider services)
    {
        var dbContext = ResolveDbContext(services, options.DbContextType);
        var discovery = orchestrator.GetDiscoveryResult(dbContext);
        var metadata = orchestrator.FindEntityMetadata(discovery, entity);
        if (metadata is null)
        {
            return RenderMissingEntityResult(options, orchestrator, discovery, entity);
        }

        var key = orchestrator.TryReadKey(dbContext, metadata, id);
        if (key is null)
        {
            return Results.NotFound();
        }

        var values = await ReadFormAsync(request);
        if (!EfUiRequestForgery.ValidateRequest(values, request.HttpContext, options.RoutePrefix, options.AntiforgeryKeyDirectory))
        {
            return Results.BadRequest();
        }

        var result = await orchestrator.DeleteAsync(dbContext, entity, key);
        if (!result.IsSuccess)
        {
            if (result.Errors.TryGetValue("id", out var idErrors) && idErrors.Contains("Row not found."))
            {
                return Results.NotFound();
            }

            return Results.BadRequest(result.Errors);
        }

        var parsed = TableQueryRequestParser.Parse(request);
        var view = await orchestrator.BuildRenderedListViewAsync(
            options.RoutePrefix,
            dbContext,
            metadata,
            parsed.Query,
            parsed.Errors,
            orchestrator.GetRenderableIssueMessages(discovery, entity),
            cancellationToken: request.HttpContext.RequestAborted);
        var canMutate = CanMutate(options, request.HttpContext.User);
        var antiForgeryToken = canMutate
            ? EfUiRequestForgery.GetOrCreateRequestToken(request.HttpContext, options.RoutePrefix, options.AntiforgeryKeyDirectory)
            : null;
        var html = orchestrator.RenderListPage(options.RoutePrefix, metadata, view, canMutate, antiForgeryToken);
        return Results.Content(html, HtmlContentType);
    }

    private static async Task<IResult> CreateFailureResultAsync(
        EfUiOptions options,
        IEfUiFlowOrchestrator orchestrator,
        DbContext dbContext,
        EntityDiscoveryResult discovery,
        string entity,
        CrudOperationResult result,
        object? key,
        bool isCreate,
        IReadOnlyDictionary<string, string[]> submittedValues,
        HttpContext httpContext)
    {
        if (result.Errors.ContainsKey("entity") || result.Errors.ContainsKey("id"))
        {
            return Results.NotFound();
        }

        var metadata = orchestrator.FindEntityMetadata(discovery, entity);
        if (metadata is null)
        {
            return RenderMissingEntityResult(options, orchestrator, discovery, entity);
        }

        var form = await orchestrator.PrepareFormAsync(dbContext, metadata, key, isCreate, submittedValues);
        if (form is null)
        {
            return Results.NotFound();
        }

        var antiForgeryToken = EfUiRequestForgery.GetOrCreateRequestToken(httpContext, options.RoutePrefix, options.AntiforgeryKeyDirectory);
        var html = orchestrator.RenderFormPage(
            options.RoutePrefix,
            metadata,
            form,
            result.Errors,
            submittedValues,
            antiForgeryToken);
        return Results.Content(html, HtmlContentType, statusCode: StatusCodes.Status400BadRequest);
    }

    private static IResult RenderMissingEntityResult(EfUiOptions options, IEfUiFlowOrchestrator orchestrator, EntityDiscoveryResult discovery, string entity)
    {
        var blockingIssues = orchestrator.GetBlockingIssueMessages(discovery, entity);
        return blockingIssues.Count > 0
            ? Results.Content(orchestrator.RenderErrorPage(options.RoutePrefix, entity, blockingIssues), HtmlContentType, statusCode: StatusCodes.Status400BadRequest)
            : Results.NotFound();
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

        builder.RequireAuthorization(new AuthorizeAttribute { Roles = options.EditRoleName });
    }

    private static bool CanMutate(EfUiOptions options, ClaimsPrincipal user)
        => !options.RequireAuthorization || user.IsInRole(options.EditRoleName);

    private static DbContext ResolveDbContext(IServiceProvider services, Type dbContextType)
        => (DbContext)services.GetRequiredService(dbContextType);

    private static async Task<Dictionary<string, string[]>> ReadFormAsync(HttpRequest request)
    {
        var form = await request.ReadFormAsync();
        return form.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Select(value => value ?? string.Empty).ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }
}
