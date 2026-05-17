# EF UI PoC Design

**Date:** 2026-05-17

## Objective

Design a proof of concept for a pluggable ASP.NET Core UI that inspects an EF Core `DbContext` and exposes a lightweight, Swagger-like CRUD interface. The first delivery focuses on scalar CRUD, TDD-first implementation, and a smooth future-facing middleware integration surface.

## Validated Decisions

- Use a **test-first core library + minimal host** structure.
- Deliver **scalar CRUD first**.
- Start with a **small demo model backed by SQLite**.
- Add a **Chinook-backed host later if simple enough**, but do not let it complicate the first slice.
- Preserve a polished final integration shape centered on **middleware-style app configuration**.

## Repository Shape

```text
src/
  EfUi.Core/
  EfUi.AspNetCore/
  EfUi.SampleHost/
  EfUi.ChinookHost/        (optional later)
tests/
  EfUi.Core.Tests/
  EfUi.AspNetCore.Tests/
docs/plans/
```

### Responsibilities

- **EfUi.Core**
  - EF model inspection
  - entity and property metadata
  - editable-field filtering
  - scalar value formatting and parsing
  - CRUD orchestration
  - HTML rendering helpers
- **EfUi.AspNetCore**
  - exposes `UseEfUi(...)`
  - translates HTTP requests into core service calls
  - keeps ASP.NET plumbing thin
- **EfUi.SampleHost**
  - runnable SQLite demo app
  - small sample model used for TDD and manual verification
- **EfUi.AspNetCore.Tests**
  - end-to-end route and CRUD behavior checks

## First Proven Feature Slice

### Routes

- `GET /efui`
- `GET /efui/{entity}`
- `GET /efui/{entity}/new`
- `GET /efui/{entity}/{id}/edit`
- `POST /efui/{entity}`
- `POST /efui/{entity}/{id}`
- `POST /efui/{entity}/{id}/delete`

### Supported Field Categories

1. **Editable scalar fields**
   - `string`
   - numbers
   - `bool`
   - `DateTime`
   - enums
   - nullable scalar equivalents
2. **Read-only scalar fields**
   - primary keys
   - store-generated values
   - properties without usable setters
3. **Navigation/display-only fields**
   - not editable in the first slice
   - may appear later in list pages as display-only values

## Architectural Boundary

The key rule is: **the middleware package must not own the business logic**.

The ASP.NET integration layer should:
- resolve the configured `DbContext`
- map route segments to entity metadata
- invoke core CRUD and rendering services
- return HTML pages or fragments

The core layer should own:
- entity discovery
- writable field selection
- form binding/parsing
- CRUD behavior
- HTML generation rules

This keeps the public integration smooth without sacrificing testability or future flexibility.

## Request Flow

1. Request enters the EfUi ASP.NET integration.
2. `DbContext` is resolved from DI using configured options.
3. Entity metadata is resolved from the route.
4. Core query/CRUD services load or mutate data.
5. HTML renderer generates page or fragment output.
6. Response is returned either as a full page or HTMX-friendly partial.

## UI Strategy

Keep the UI server-rendered and minimal:
- no SPA or frontend build pipeline
- light HTMX usage only
- normal HTML forms still work when HTMX is absent
- layout remains intentionally plain for the PoC

### HTMX Usage

- `hx-post` for create/update/delete
- table region refresh after writes where practical
- graceful fallback to redirect-based flows

## Demo Data Strategy

### First Demo Model

Use a small SQLite-backed model:

```csharp
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? GroupId { get; set; }
    public Group? Group { get; set; }
}

public class Group
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<User> Users { get; set; } = new();
}
```

Why this model:
- enough scalar fields to prove form rendering and binding
- a visible relationship exists for later display work
- simple enough to keep the first TDD loop focused

### Later Demo Validation

Optionally add a second host backed by `db/chinook.db` once the first slice is stable.

## Testing Strategy

### 1. Core Unit Tests

Cover:
- entity discovery from EF Core model
- route-name to entity resolution
- editable scalar property filtering
- exclusion of keys/read-only/navigation properties from forms
- scalar formatting to HTML-safe strings
- parsing form values into typed CLR values

### 2. Service-Level Tests

With SQLite-backed tests, verify:
- create persists a row
- edit updates writable fields only
- delete removes a row
- invalid values return structured errors
- unknown entities and missing rows are handled cleanly

### 3. HTTP Integration Tests

Verify:
- `/efui` lists entities
- `/efui/users` renders a table
- create/edit/delete flows succeed
- HTMX and non-HTMX flows both behave sensibly

## Middleware Surface

The public API should stay aligned with the design target:

```csharp
app.UseEfUi(options =>
{
    options.DbContextType = typeof(SampleDbContext);
    options.RoutePrefix = "/efui";
    options.EnableInProduction = false;
    options.EntityFilter = entity => true;
    options.PropertyFilter = property => true;
});
```

Internally, this should delegate into services such as:
- `IEntityMetadataProvider`
- `IEntityCrudService`
- `IHtmlPageRenderer`

Whether the implementation uses middleware primitives, mapped branches, or endpoint helpers internally is secondary. The important part is keeping the external integration smooth and stable.

## Error Handling Rules

- unknown entity: `404`
- missing row: `404`
- invalid posted scalar value: `400` with validation feedback
- unsupported property type: omitted from edit forms
- production environment with feature disabled: no routes exposed

## Acceptance Criteria

- tests run successfully from one command
- sample host runs from one command
- `/efui` shows discovered entities
- user can create, edit, and delete rows for at least one entity
- forms omit non-editable fields
- architecture still supports a polished `UseEfUi(...)` integration surface

## Implementation Order

1. scaffold solution and projects
2. write failing metadata tests
3. implement metadata and scalar editability rules
4. write failing binder and CRUD tests
5. implement minimal binder and CRUD services
6. write failing renderer tests
7. implement minimal HTML rendering
8. add failing ASP.NET integration tests
9. implement thin `UseEfUi(...)` integration
10. wire sample SQLite host
11. optionally add Chinook-backed demo host if still simple
