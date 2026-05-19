namespace EfUi.Core.Rendering;

public sealed record TableQueryField(
    string Name,
    bool IsFilterable,
    bool IsSortable,
    IReadOnlyList<string>? supportedOperators = null)
{
    public IReadOnlyList<string> SupportedOperators { get; init; } = supportedOperators ?? ["contains", "eq"];
}
