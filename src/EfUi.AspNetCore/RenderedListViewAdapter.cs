using EfUi.Core.Metadata;
using EfUi.Core.Query;
using EfUi.Core.Rendering;

namespace EfUi.AspNetCore;

internal static class RenderedListViewAdapter
{
    public static RenderedListView Create(
        string routePrefix,
        EntityMetadata metadata,
        EntityListQueryResult result,
        IReadOnlyList<string>? parserErrors = null,
        IReadOnlyList<string>? warnings = null)
        => RenderedListViewFactory.Create(routePrefix, metadata, result, parserErrors, warnings);
}
