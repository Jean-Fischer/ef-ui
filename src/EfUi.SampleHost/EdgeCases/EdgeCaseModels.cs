using EfUi.Core.Metadata;

namespace EfUi.SampleHost.EdgeCases;

[EfUiDisplayColumn(nameof(EdgeCaseCustomer.Name))]
public sealed class EdgeCaseCustomer
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class EdgeCaseOrder
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public int BillingCustomerId { get; set; }

    [EfUiDisplayColumn(nameof(EdgeCaseCustomer.Code))]
    public EdgeCaseCustomer BillingCustomer { get; set; } = null!;

    public int ShippingCustomerId { get; set; }
    public EdgeCaseCustomer ShippingCustomer { get; set; } = null!;
    public decimal Total { get; set; }
}

public sealed class EdgeCaseInvoice
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public decimal Amount { get; set; }
}

public sealed class EdgeCaseCompositeParent
{
    public string Code { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class EdgeCaseCompositeChild
{
    public int Id { get; set; }
    public string ParentCode { get; set; } = string.Empty;
    public string ParentRegion { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public EdgeCaseCompositeParent Parent { get; set; } = null!;
}

public sealed class EdgeCaseShadowNote
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public sealed class EdgeCaseMembership
{
    public int UserId { get; set; }
    public int GroupId { get; set; }
    public string Label { get; set; } = string.Empty;
}

public sealed class EdgeCaseReportRow
{
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
