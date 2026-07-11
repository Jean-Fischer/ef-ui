using EfUi.Core.Orchestration;

namespace EfUi.AspNetCore;

public sealed class EfUiOptions
{
    public Type DbContextType { get; set; } = null!;

    public string RoutePrefix { get; set; } = "/efui";

    public bool EnableInProduction { get; set; }

    public bool RequireAuthorization { get; set; }

    public string ReadOnlyRoleName { get; set; } = "ReadOnly";

    public string EditRoleName { get; set; } = "Edit";

    public string? AntiforgeryKeyDirectory { get; set; }

    /// <summary>
    /// Optional application flow module. When omitted, EF UI creates its default orchestrator once per mount.
    /// </summary>
    public IEfUiFlowOrchestrator? FlowOrchestrator { get; set; }
}
