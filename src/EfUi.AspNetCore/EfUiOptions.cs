using Microsoft.EntityFrameworkCore.Metadata;

namespace EfUi.AspNetCore;

public sealed class EfUiOptions
{
    public Type DbContextType { get; set; } = null!;

    public string RoutePrefix { get; set; } = "/efui";

    public bool EnableInProduction { get; set; }

    public Func<IMutableEntityType, bool>? EntityFilter { get; set; }

    public Func<IProperty, bool>? PropertyFilter { get; set; }
}
