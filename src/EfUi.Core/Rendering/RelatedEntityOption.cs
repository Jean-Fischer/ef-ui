namespace EfUi.Core.Rendering;

public sealed record RelatedEntityOption(
    string Value,
    string Label,
    bool Selected = false,
    bool Disabled = false,
    string? Description = null);
