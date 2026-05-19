using Microsoft.EntityFrameworkCore;

namespace EfUi.SampleHost.EdgeCases;

public sealed class EdgeCaseDbContext(DbContextOptions<EdgeCaseDbContext> options) : DbContext(options)
{
    public DbSet<EdgeCaseCustomer> Customers => Set<EdgeCaseCustomer>();
    public DbSet<EdgeCaseOrder> Orders => Set<EdgeCaseOrder>();
    public DbSet<EdgeCaseInvoice> Invoices => Set<EdgeCaseInvoice>();
    public DbSet<EdgeCaseCompositeParent> CompositeParents => Set<EdgeCaseCompositeParent>();
    public DbSet<EdgeCaseCompositeChild> CompositeChildren => Set<EdgeCaseCompositeChild>();
    public DbSet<EdgeCaseShadowNote> ShadowNotes => Set<EdgeCaseShadowNote>();
    public DbSet<EdgeCaseMembership> Memberships => Set<EdgeCaseMembership>();
    public DbSet<EdgeCaseReportRow> Reports => Set<EdgeCaseReportRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EdgeCaseCustomer>(builder =>
        {
            builder.ToTable("customers");
            builder.HasKey(x => x.Id);
        });

        modelBuilder.Entity<EdgeCaseOrder>(builder =>
        {
            builder.ToTable("orders");
            builder.HasKey(x => x.Id);
            builder.HasOne(x => x.BillingCustomer)
                .WithMany()
                .HasForeignKey(x => x.BillingCustomerId);
            builder.HasOne(x => x.ShippingCustomer)
                .WithMany()
                .HasForeignKey(x => x.ShippingCustomerId);
        });

        modelBuilder.Entity<EdgeCaseInvoice>(builder =>
        {
            builder.ToTable("invoices");
            builder.HasKey(x => x.Id);
            builder.HasOne<EdgeCaseCustomer>()
                .WithMany()
                .HasForeignKey(x => x.CustomerId);
        });

        modelBuilder.Entity<EdgeCaseCompositeParent>(builder =>
        {
            builder.ToTable("composite_parents");
            builder.HasKey(x => new { x.Code, x.Region });
        });

        modelBuilder.Entity<EdgeCaseCompositeChild>(builder =>
        {
            builder.ToTable("composite_children");
            builder.HasKey(x => x.Id);
            builder.HasOne(x => x.Parent)
                .WithMany()
                .HasForeignKey(x => new { x.ParentCode, x.ParentRegion });
        });

        modelBuilder.Entity<EdgeCaseShadowNote>(builder =>
        {
            builder.ToTable("shadow_notes");
            builder.HasKey(x => x.Id);
            builder.Property<int>("CustomerId");
            builder.HasOne<EdgeCaseCustomer>()
                .WithMany()
                .HasForeignKey("CustomerId");
        });

        modelBuilder.Entity<EdgeCaseMembership>(builder =>
        {
            builder.ToTable("memberships");
            builder.HasKey(x => new { x.UserId, x.GroupId });
        });

        modelBuilder.Entity<EdgeCaseReportRow>(builder =>
        {
            builder.ToTable("reports");
            builder.HasNoKey();
        });
    }
}
