namespace EfUi.AspNetCore;

public sealed class EfUiOptions
{
    public Type DbContextType { get; set; } = null!;

    public string RoutePrefix { get; set; } = "/efui";

    public bool EnableInProduction { get; set; }
}
