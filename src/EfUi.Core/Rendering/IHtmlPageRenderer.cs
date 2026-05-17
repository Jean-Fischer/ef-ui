using EfUi.Core.Metadata;

namespace EfUi.Core.Rendering;

public interface IHtmlPageRenderer
{
    string RenderIndex(string routePrefix, IReadOnlyList<EntityMetadata> entities);
    string RenderForm(string routePrefix, EntityMetadata entity, object? model, bool isCreate, IReadOnlyDictionary<string, string[]> errors);
}
