using EfUi.Core.Crud;
using EfUi.Core.Metadata;
using EfUi.Core.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EfUi.Core.Orchestration;

/// <summary>
/// Owns the application-level preparation shared by EF UI routes.
/// HTTP binding, authorization, antiforgery, and HTTP result adaptation remain at the ASP.NET seam.
/// </summary>
public interface IEfUiFlowOrchestrator
{
    EntityDiscoveryResult GetDiscoveryResult(DbContext dbContext);

    EntityMetadata? FindEntityMetadata(EntityDiscoveryResult discovery, string routeName);

    IReadOnlyList<string> GetRenderableIssueMessages(EntityDiscoveryResult discovery, string? routeName = null);

    IReadOnlyList<string> GetBlockingIssueMessages(EntityDiscoveryResult discovery, string? routeName = null);

    Task<RenderedListView> BuildRenderedListViewAsync(
        string routePrefix,
        DbContext dbContext,
        EntityMetadata metadata,
        TableQuery query,
        IReadOnlyList<string>? parserErrors = null,
        IReadOnlyList<string>? warnings = null,
        bool includeAllRows = false,
        CancellationToken cancellationToken = default);

    Task<PreparedEntityForm?> PrepareFormAsync(
        DbContext dbContext,
        EntityMetadata metadata,
        object? key,
        bool isCreate,
        IReadOnlyDictionary<string, string[]>? submittedValues = null);

    object? TryReadKey(DbContext dbContext, EntityMetadata metadata, string rawKey);

    IReadOnlyDictionary<string, string[]> EnsureCollectionFieldsPresent(
        EntityMetadata metadata,
        IReadOnlyDictionary<string, string[]> submittedValues,
        bool isCreate);

    Task<CrudOperationResult> CreateAsync(
        DbContext dbContext,
        string entityRoute,
        IReadOnlyDictionary<string, string?> values);

    Task<CrudOperationResult> CreateAsync(
        DbContext dbContext,
        string entityRoute,
        IReadOnlyDictionary<string, string[]> values);

    Task<CrudOperationResult> UpdateAsync(
        DbContext dbContext,
        string entityRoute,
        object key,
        IReadOnlyDictionary<string, string?> values);

    Task<CrudOperationResult> UpdateAsync(
        DbContext dbContext,
        string entityRoute,
        object key,
        IReadOnlyDictionary<string, string[]> values);

    Task<CrudOperationResult> DeleteAsync(
        DbContext dbContext,
        string entityRoute,
        object key);

    string RenderIndexPage(string routePrefix, EntityDiscoveryResult discovery);

    string RenderListPage(
        string routePrefix,
        EntityMetadata metadata,
        RenderedListView view,
        bool showActions,
        string? antiForgeryToken);

    string RenderFormPage(
        string routePrefix,
        EntityMetadata metadata,
        PreparedEntityForm form,
        IReadOnlyDictionary<string, string[]> errors,
        IReadOnlyDictionary<string, string[]>? submittedValues,
        string? antiForgeryToken);

    string RenderErrorPage(string routePrefix, string title, IReadOnlyList<string> messages);
}

public sealed record PreparedEntityForm(
    object? Model,
    object? Key,
    IReadOnlyDictionary<string, IReadOnlyList<RelatedEntityOption>> FieldOptions);
