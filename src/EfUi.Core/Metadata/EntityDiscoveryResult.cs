namespace EfUi.Core.Metadata;

public sealed record EntityDiscoveryResult(
    IReadOnlyList<EntityMetadata> Entities,
    IReadOnlyList<EntityDiscoveryIssue> Issues)
{
    public IReadOnlyList<EntityDiscoveryIssue> RenderableIssues(string? routeName = null)
        => Issues.Where(issue => issue.CanRender && (routeName is null || string.Equals(issue.RouteName, routeName, StringComparison.OrdinalIgnoreCase))).ToList();

    public IReadOnlyList<EntityDiscoveryIssue> BlockingIssues(string routeName)
        => Issues.Where(issue => !issue.CanRender && string.Equals(issue.RouteName, routeName, StringComparison.OrdinalIgnoreCase)).ToList();
}
