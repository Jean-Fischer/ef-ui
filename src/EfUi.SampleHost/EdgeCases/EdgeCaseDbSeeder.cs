using Microsoft.EntityFrameworkCore;

namespace EfUi.SampleHost.EdgeCases;

public static class EdgeCaseDbSeeder
{
    public static async Task SeedAsync(EdgeCaseDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        if (await db.Customers.AnyAsync())
        {
            return;
        }

        var alpha = new EdgeCaseCustomer { Code = "ALP", Name = "Alpha Corp" };
        var beta = new EdgeCaseCustomer { Code = "BET", Name = "Beta Labs" };

        db.Customers.AddRange(alpha, beta);
        await db.SaveChangesAsync();

        db.Orders.AddRange(
            new EdgeCaseOrder
            {
                Number = "ORD-1001",
                BillingCustomer = alpha,
                ShippingCustomer = beta,
                Total = 125m
            },
            new EdgeCaseOrder
            {
                Number = "ORD-1002",
                BillingCustomer = beta,
                ShippingCustomer = alpha,
                Total = 250m
            });

        db.Invoices.AddRange(
            new EdgeCaseInvoice
            {
                Number = "INV-2001",
                CustomerId = alpha.Id,
                Amount = 42m
            },
            new EdgeCaseInvoice
            {
                Number = "INV-2002",
                CustomerId = beta.Id,
                Amount = 84m
            });

        db.CompositeParents.AddRange(
            new EdgeCaseCompositeParent { Code = "A", Region = "US", Name = "North America A" },
            new EdgeCaseCompositeParent { Code = "B", Region = "EU", Name = "Europe B" });

        db.CompositeChildren.AddRange(
            new EdgeCaseCompositeChild
            {
                Description = "Composite child 1",
                ParentCode = "A",
                ParentRegion = "US"
            },
            new EdgeCaseCompositeChild
            {
                Description = "Composite child 2",
                ParentCode = "B",
                ParentRegion = "EU"
            });

        var shadowNote = new EdgeCaseShadowNote
        {
            Title = "Shadow FK note",
            Body = "This row uses a shadow CustomerId foreign key."
        };
        db.ShadowNotes.Add(shadowNote);
        db.Entry(shadowNote).Property("CustomerId").CurrentValue = alpha.Id;

        db.Memberships.AddRange(
            new EdgeCaseMembership { UserId = 1, GroupId = 1, Label = "Primary membership" },
            new EdgeCaseMembership { UserId = 2, GroupId = 1, Label = "Secondary membership" });

        await db.SaveChangesAsync();
    }
}
