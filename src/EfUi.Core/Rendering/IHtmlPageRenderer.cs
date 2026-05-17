using EfUi.Core.Metadata;

namespace EfUi.Core.Rendering;

public interface IHtmlPageRenderer
{
    string RenderIndex(string routePrefix, IReadOnlyList<EntityMetadata> entities);
    string RenderList(string routePrefix, EntityMetadata entity, IReadOnlyList<object> rows);
    string RenderForm(string routePrefix, EntityMetadata entity, object? model, bool isCreate, IReadOnlyDictionary<string, string[]> errors);
    string RenderEditForm(string routePrefix, EntityMetadata entity, object? model, bool isCreate, IReadOnlyDictionary<string, string[]> errors, object? key);
}
