namespace EfUi.Core.Metadata;

public sealed record EntityDiscoveryIssue(
    string RouteName,
    string Message,
    bool CanRender = true);
