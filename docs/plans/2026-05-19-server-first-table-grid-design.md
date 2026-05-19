# Server-First Table Grid Design

**Date:** 2026-05-19

## Objective

Replace the current plain readonly list table with a stronger table foundation that supports:

- server-side filtering and sorting
- a visible query-builder UI above the table
- FK cells that link to the related row edit page
- “related rows” navigation that opens the child table with a normal editable pre-applied filter
- a future path to virtual scrolling / windowed data loading

The first version should stay compatible with EF UI’s current ASP.NET server-rendered architecture, keep the business/query contract owned by EF UI rather than by a client library, and avoid introducing an overly broad query language.

## Validated Decisions

- Use a **server-first** filtering and sorting model.
- Choose a solution that keeps a **credible future path to virtual scrolling**.
- Prefer a **built-in table UX** over a fully hand-rolled solution, while still keeping the dependency as lightweight as practical.
- Allow users to add filters and sorts **on the fly** from the UI.
- Restrict filtering/sorting to **fields intentionally exposed by the table**.
- For FK-backed columns, filtering/sorting should target the **displayed FK label semantics** already shown in the table, not arbitrary deep relationship traversal.
- Use a **query-builder bar above the table** instead of header filters for the primary UX.
- Preserve a **server-rendered HTML-first path** initially, while allowing a richer JS-owned table rendering path later if needed.
- FK cells in list tables should link to the related row’s **edit page** for now.
- “Related rows” navigation should open the child table with a **visible editable filter already applied**.
- Use a **small custom URL/query contract** rather than adopting full **OData** in v1.

## Evaluated Approaches

### Option A — Tabulator-backed server-first grid (**recommended**)

Use Tabulator as the enhancement layer for list tables while EF UI owns the query contract, data semantics, and server-side execution.

**Pros**
- strong match for remote/server filtering and sorting
- built-in path to virtualization through Tabulator’s virtual DOM model
- supports custom cell rendering for FK links and action cells
- lets EF UI add a separate query-builder bar without forcing all UX into header filters
- better long-term grid foundation than a minimal DOM enhancer

**Cons**
- heavier than a tiny progressive-enhancement helper
- can pull rendering concerns toward the JS library if the boundary is not designed carefully
- requires a richer row/column contract than the current string-only renderer model

### Option B — Grid.js with server-side integration

Use Grid.js as a simpler, lighter JS table layer with server-side sort/search/pagination.

**Pros**
- clean vanilla setup
- open source and maintained
- simpler mental model than a full-featured data grid

**Cons**
- weaker virtualization story for the stated roadmap
- less compelling fit once future windowed scrolling becomes a requirement
- more likely to be outgrown if richer grid behaviors are needed

### Option C — HTMX / hyperscript / server HTML only

Keep the table mostly server-rendered and implement filtering, sorting, and progressive loading with custom hypermedia interactions.

**Pros**
- strongest fit with the current architecture
- smallest dependency footprint
- URL-driven state maps naturally to server-rendered pages and partial refreshes

**Cons**
- filter/sort UX becomes custom work
- no maintained table/grid abstraction to inherit from
- virtualization would also become custom work, increasing long-term cost

## Recommended Direction

Adopt **Option A** with a strict architecture boundary:

- **EF UI owns the canonical query model**
- **EF UI owns server-side execution against EF**
- **EF UI owns related-row semantics and FK label behavior**
- **Tabulator is only the table enhancement/rendering layer**

This keeps the design aligned with the project’s server-rendered roots while still choosing a table component that can grow into virtual scrolling later.

## Canonical Query Model

Define one internal query object for list screens. The first version should support:

- `filters[]` → `{ field, operator, value }`
- `sorts[]` → `{ field, direction }`
- `offset`
- `limit`

Example:

```csharp
public sealed record TableQuery(
    IReadOnlyList<TableFilter> Filters,
    IReadOnlyList<TableSort> Sorts,
    int Offset,
    int Limit);

public sealed record TableFilter(string Field, string Operator, string? Value);
public sealed record TableSort(string Field, string Direction);
```

This should be EF UI’s canonical representation regardless of which client table widget is used.

### Why not full OData?

OData is relevant conceptually because it proves queryable table state can be encoded in URLs, but it is not the right primary contract for v1.

Reasons:
- broader than the current UI needs
- larger validation and security surface
- leaks a generic query language into a narrowly scoped admin/table UX
- unnecessary complexity for field/operator/value rules on exposed columns

If interoperability becomes important later, EF UI can add an **adapter** from the internal `TableQuery` model to an OData-facing API boundary without making the UI itself depend on OData syntax.

## URL Contract

Represent the table state in normal query parameters so state is bookmarkable and shareable.

Suggested shape:

```text
?filter.0.field=Artist
&filter.0.op=contains
&filter.0.value=AC
&sort.0.field=Title
&sort.0.dir=asc
&offset=0
&limit=50
```

This contract should be used for:
- user-created filters and sorts
- deep links to table states
- “related rows” navigation
- future virtual scrolling/window requests

The server should parse this URL contract into the canonical `TableQuery` object and reject unsupported fields/operators cleanly.

## UI Shape

Each list page should have two layers:

1. **query-builder bar**
2. **table surface**

### Query-builder bar

The bar should be the primary interaction surface. It should allow users to:

- add a filter
- remove a filter
- add a sort
- remove a sort
- change sort direction
- see all currently active rules

The same visible UI should show:
- user-authored rules
- rules added by navigating from another screen

This keeps pre-applied related-row filters understandable and editable instead of feeling like a hidden special mode.

### Table surface

The table surface should show the current result window and row actions. The first version can remain compatible with a real HTML table shell or server-rendered fallback, but the model should be rich enough for a JS grid to render from the same data.

## FK Column Behavior

For FK-backed columns already displayed with human-readable labels, list cells should render as:

- visible label text
- optional link target to the related row edit page

Example:
- Album list `Artist` cell displays `AC/DC`
- clicking it goes to `/artists/1/edit`

This means the renderer needs richer cell metadata than a plain string. A future row model should support at least:

- `text`
- `href` (optional)
- raw value / key metadata when needed for client integration

## Related Rows Navigation

When the user opens a child table from a parent context, EF UI should navigate to the child list with a normal visible filter already present in the URL and the query-builder UI.

Example:
- Artist 1 page → Albums table
- destination table loads with a visible rule equivalent to `Artist = AC/DC`

Important behavior:
- the rule should be editable
- the rule should be removable
- the page should behave exactly like any other filtered list afterwards

This should apply both to row-to-related navigation and to the existing “related rows” concept on parent pages.

## Filtering and Sorting Scope

The first version should be intentionally constrained.

Supported scope:
- fields exposed as visible table columns
- FK label columns using the display semantics already chosen for the table

Not in scope for v1:
- arbitrary relationship-path expressions
- free-form query language entry
- generic cross-entity query building

This keeps the model secure, understandable, and aligned with the actual UX.

## Data Flow

### Request flow

1. Browser requests a list page with URL query parameters.
2. Server binds the query parameters into `TableQuery`.
3. Server validates fields, operators, sort directions, and window arguments.
4. Server translates the query into EF operations.
5. Server resolves display labels and FK edit links for visible cells.
6. Server renders the list shell, query-builder state, and current result window.
7. Optional JS enhancement layer upgrades the table behavior.

### Related rows flow

1. User clicks a parent-context related-row link.
2. Link points to the child entity list with a pre-applied filter in the query string.
3. Server binds and executes that filter like any other table query.
4. Query-builder bar shows the rule visibly.
5. User may edit or remove the rule and continue exploring normally.

### Future virtual scrolling flow

1. Client requests a window with `offset` and `limit`.
2. Server returns only that result slice plus total/result metadata as needed.
3. The client table layer renders the visible window.
4. Further scrolling loads the next window using the same canonical query model.

## Progressive Enhancement Strategy

The first release should not require a big-bang rewrite.

Recommended sequence:

1. enrich the list rendering model beyond `RenderedListRow` string cells
2. add URL → `TableQuery` parsing and validation
3. add query-builder bar rendering
4. add server-side filter/sort execution
5. add FK link metadata and related-row prefilter links
6. integrate Tabulator as a replaceable enhancement layer
7. add windowed loading / virtual scrolling later on top of the same contract

This keeps EF UI in control of semantics and reduces lock-in to any one JS grid.

## Error Handling and Safety

### Unsupported field or operator

If the URL specifies a field or operator not exposed by the current table definition, the server should reject it safely.

Preferred initial behavior:
- ignore invalid rules for rendering only if that is clearly surfaced to the user, or
- reject with a visible validation message in the table UI

Silent acceptance of unsupported semantics should be avoided.

### Invalid values

If a filter value cannot be bound or interpreted for the chosen field/operator pair, the page should render with a visible error state rather than throwing.

### Missing related entity

If an FK value exists but the related row cannot be loaded, the cell should fall back to the current display text behavior without breaking the table.

### JavaScript unavailable

Without JS enhancement, the page should still be renderable and understandable, even if some richer table behaviors are unavailable or simplified.

## Testing Strategy

### Query binding tests

Add tests for:
- URL query string → `TableQuery`
- multiple filters and sorts
- offset/limit parsing
- rejection of unsupported fields/operators/directions

### Query execution tests

Add tests for:
- server-side `contains` / equality filtering on visible columns
- server-side sorting on visible columns
- FK label-based filtering/sorting on supported displayed FK columns
- stable behavior when related rows are missing

### Renderer tests

Add or update tests to verify:
- query-builder bar renders current filters and sorts
- FK cells render as links to edit pages
- row action cells still render correctly
- related-row links produce the intended destination/query state

### ASP.NET endpoint tests

Use the sample host / Chinook flows to verify:
- list pages accept URL-driven filters/sorts
- related-row navigation opens child lists with visible editable filters
- FK columns render human-readable linked labels
- non-JS fallback still returns usable HTML

### Manual verification

Verify in the browser:
- adding/removing filters updates the visible query state correctly
- sorting changes are reflected in the URL and results
- opening related rows produces a normal editable filtered list
- FK links navigate to edit pages
- the chosen table library enhancement does not break fallback HTML

## Success Criteria

The design is successful when all of the following are true:

- list pages have a visible query-builder bar above the table
- users can add and remove safe server-side filters and sorts
- table state is bookmarkable and shareable through URL parameters
- FK label cells can link to related edit pages
- related-row navigation opens a child table with a visible editable pre-applied filter
- EF UI owns a small stable query contract independent of the client library
- the design remains compatible with a future virtual scrolling / windowed-loading implementation
- full OData is not required to deliver the intended UX
