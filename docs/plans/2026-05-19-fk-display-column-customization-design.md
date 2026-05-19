# FK Display Column Customization Design

## Goal

Allow users to choose which property is shown for foreign-key related values, while keeping the current default behavior as the fallback. The feature should remain lightweight, avoid fluent API, and support per-FK overrides when the same principal entity is shown through different relationships.

## Validated decisions

- Add an explicit EF UI attribute for display-column customization.
- Support both class-level defaults and per-navigation-property overrides.
- Prefer the per-FK override when both are present.
- Preserve the current heuristic fallback (`Name`, `Title`, `Email`, then primary key).
- Ignore invalid or unsupported configuration safely.
- Keep the feature attribute-based rather than requiring fluent API or partial configuration classes.

## Recommended approach

Use a single attribute that can be applied in two places:

```csharp
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class EfUiDisplayColumnAttribute : Attribute
{
    public EfUiDisplayColumnAttribute(string propertyName)
    {
        PropertyName = propertyName;
    }

    public string PropertyName { get; }
}
```

### Where it can be applied

#### 1. Navigation property override

This is the most specific and most useful form. It lets a dependent entity choose a display field per FK relationship.

```csharp
public class Order
{
    public int BillingCustomerId { get; set; }

    [EfUiDisplayColumn(nameof(Customer.Code))]
    public Customer BillingCustomer { get; set; } = default!;

    public int ShippingCustomerId { get; set; }

    [EfUiDisplayColumn(nameof(Customer.Name))]
    public Customer ShippingCustomer { get; set; } = default!;
}
```

#### 2. Principal entity default

This is the broad default when no relationship-specific override exists.

```csharp
[EfUiDisplayColumn(nameof(Customer.Name))]
public class Customer
{
}
```

## Resolution order

When EF UI needs to show a related value, it should resolve the display column in this order:

1. navigation-property attribute on the dependent FK navigation
2. entity-level attribute on the principal entity type
3. current heuristic fallback:
   - `Name`
   - `Title`
   - `Email`
4. primary key value

That order keeps existing behavior intact while making the override story explicit and predictable.

## Runtime flow

The runtime should resolve the display choice during metadata discovery, not during rendering.

### Metadata discovery

`EfEntityMetadataProvider` already inspects entity relationships and property metadata. It should be extended to:

- inspect dependent navigation properties for `EfUiDisplayColumnAttribute`
- inspect the principal entity type for a class-level `EfUiDisplayColumnAttribute`
- resolve the final display property name once
- store that value in the metadata model

A suggested metadata shape is a new nullable field such as:

- `RelatedDisplayPropertyName`

That keeps rendering code simple because the renderer only consumes a resolved value instead of re-reading attributes.

### Rendering and labels

The same resolved display property should be used everywhere a foreign-key-related value appears:

- list table cell text
- list table cell links
- related-row picker labels
- FK display values used in filtering/search

That ensures consistent UX. If a user configures `BillingCustomer` to show `Code`, they should see `Code` wherever that relationship is rendered.

## Error handling and safety

The feature should be conservative and fail open.

- If the attribute names a property that does not exist, ignore it and continue to the next fallback.
- If the attribute is applied to a class or property that is not part of a mapped relationship, ignore it.
- If the resolved property value is empty for a given row, continue to the heuristic or primary-key fallback.
- Do not throw on bad configuration in normal rendering paths.

This keeps the feature safe for incremental adoption.

## Why this approach

### Why not fluent API?

Fluent API would work well, but it pushes users into `OnModelCreating` and often implies partial classes or configuration-only code. That is more ceremony than this project needs.

### Why not attribute-only on the principal entity?

A class-level attribute is nice for a default, but it cannot express different display choices for different relationships to the same principal entity.

### Why this hybrid wins

The hybrid gives the user both:

- a simple default with almost no setup
- a precise per-FK override when necessary

It stays readable, lightweight, and close to the model.

## Options considered

### Option A — class-level attribute only

This would be the smallest API surface, but it cannot handle different display choices for different FKs to the same entity.

### Option B — fluent API only

This is flexible, but it adds configuration overhead and does not fit the project’s lightweight style.

### Option C — hybrid attribute model (**recommended**)

Supports defaults and per-FK overrides while staying simple and discoverable.

## Testing strategy

Add coverage for:

- navigation-property override wins over the class-level default
- class-level default applies when no FK override exists
- current heuristic remains the fallback when no attribute is present
- invalid property names fall back cleanly without breaking rendering
- list cell display and related-row picker labels remain consistent

Tests should verify both the resolved metadata and the visible rendered output.

## Non-goals

- fluent API configuration
- expression-tree based attribute syntax
- changing the existing heuristic fallback order
- adding a large configuration subsystem
- per-cell user customization in the UI

## Implementation note

This feature should be implemented by extending metadata resolution first, then reusing the resolved display column everywhere the related value is shown.
