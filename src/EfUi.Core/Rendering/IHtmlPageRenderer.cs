using EfUi.Core.Metadata;

namespace EfUi.Core.Rendering;

public interface IHtmlPageRenderer
{
    string RenderIndex(string routePrefix, IReadOnlyList<EntityMetadata> entities);
    string RenderList(string routePrefix, EntityMetadata entity, RenderedListView view);
    string RenderForm(string routePrefix, EntityMetadata entity, object? model, bool isCreate, IReadOnlyDictionary<string, string[]> errors, IReadOnlyDictionary<string, string[]>? submittedValues = null, IReadOnlyDictionary<string, IReadOnlyList<RelatedEntityOption>>? fieldOptions = null);
    string RenderEditForm(string routePrefix, EntityMetadata entity, object? model, bool isCreate, IReadOnlyDictionary<string, string[]> errors, object? key, IReadOnlyDictionary<string, string[]>? submittedValues = null, IReadOnlyDictionary<string, IReadOnlyList<RelatedEntityOption>>? fieldOptions = null);
}
