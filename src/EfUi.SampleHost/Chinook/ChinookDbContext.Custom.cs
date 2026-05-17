using Microsoft.EntityFrameworkCore;

namespace EfUi.SampleHost.Chinook;

public partial class ChinookDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<FlywaySchemaHistory>();
    }
}
