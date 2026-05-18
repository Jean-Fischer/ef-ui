# Readonly Theme and Shared Display Label Design

**Date:** 2026-05-18

## Objective

Extend the current form styling work so the generated EF UI feels visually consistent across:

- top-level entity index pages
- entity list / readonly table pages
- existing create / edit forms

At the same time, centralize the logic used to represent related entities as human-readable labels so the UI does not show raw foreign key values when a better identifying string is available.

This phase should stay lightweight, server-rendered, and convention-based.

## Validated Decisions

- Apply the current themed styling to the **top-level index page** too.
- Apply the current themed styling to **readonly/list table pages**.
- For foreign keys shown in readonly tables, use the **same identifying label logic** as everywhere else rather than showing raw FK values.
- Use a **single shared display-label convention** for now.
- Keep that convention simple: **`Name -> Title -> Email -> primary key`**.
- Do **not** introduce per-entity configuration yet.
- Prefer one shared logic path for object display wherever possible.

## Evaluated Approaches

### Option A — Patch readonly tables only

Teach list rendering to replace FK values with labels but keep the logic local to that screen.

**Pros**
- quickest possible change
- low code movement

**Cons**
- duplicates the current form-option label logic
- likely to drift over time
- does not satisfy the goal of one shared object-display path

### Option B — Shared display-label resolver plus themed readonly pages (**recommended**)

Extract one shared display-label rule and route all object-label rendering through it. Reuse it for form options, ownership messages, and readonly FK display. Theme the index and list pages with the same local stylesheet family already used by forms.

**Pros**
- consistent object display across the UI
- easier to reason about and test
- supports future UI surfaces without redoing heuristics
- keeps the implementation small and convention-based

**Cons**
- slightly broader refactor than a one-off patch
- requires some renderer/API reshaping for list pages

### Option C — Per-entity configurable display rules

Introduce explicit configuration for how each entity should be represented.

**Pros**
- most flexible
- handles special cases precisely

**Cons**
- unnecessary complexity right now
- adds API and documentation surface before a real need exists

## Recommended Design Direction

Adopt **Option B**.

Create one shared display-label resolver in the core/UI pipeline and use it everywhere an entity must be represented to a human.

The resolver should follow the validated convention:

1. `Name`
2. `Title`
3. `Email`
4. primary key value

If a preferred property exists but is null, empty, or whitespace, continue to the next fallback.

This resolver should become the default path for:

- reference dropdown option labels in edit/create forms
- chip-picker option labels
- one-to-many ownership text such as `assigned to Guests`
- readonly/list table rendering of FK-backed columns
- any future UI element that needs a human-facing label for a row

## Rendering Responsibilities

The implementation should keep responsibilities separated cleanly.

### Core / shared logic

Core should own the shared display-label rule so the convention is not buried in ASP.NET-only code.

### ASP.NET integration

The ASP.NET layer should continue to do EF-aware work:

- detect related entity metadata
- read related rows
- build lookup maps for FK value -> shared display label
- prepare the data passed to the renderer

### HTML renderer

The renderer should stay focused on HTML generation. It should render themed markup and consume already-prepared display values rather than learning EF relationship resolution rules itself.

## Readonly Table Behavior

Readonly/list pages should stop printing raw FK values whenever the system can identify the related entity.

Examples:

- `ArtistId = 1` should render as `AC/DC`
- `CustomerId = 5` should render as the customer label chosen by the shared resolver
- nullable FKs with no value should render as empty

The simplest version is to keep the visible column name unchanged for now and only replace the displayed value. A later pass can decide whether FK column headers should also become navigation-oriented labels.

To avoid repeated per-row relationship lookups, the ASP.NET layer should build a related-row label lookup per involved foreign key and reuse it across the rendered table.

## Index and List Theming

The current form theme should be extended into a unified local stylesheet that also covers:

- page shell
- page title
- page actions
- card/surface wrapper
- table layout
- table headers and rows
- inline action area for edit/delete actions
- navigation links on the index page

The visual style should remain consistent with the existing form treatment:

- neutral background
- white surfaces
- subtle borders
- modest radius
- compact spacing
- restrained admin-style presentation

This is still a local CSS theme, not Tailwind itself.

## Data Flow

### Forms

Existing form option-building should switch to the shared display-label resolver but otherwise preserve current behavior.

### Readonly tables

When building a list page:

1. inspect the entity's EF metadata for single-column foreign keys
2. identify which displayed properties are FK scalar properties
3. load related rows for those FK targets
4. map related PK values to shared display labels
5. replace the rendered cell text with the related label
6. fall back safely when lookup data is unavailable

This keeps the display behavior aligned with forms while staying efficient and predictable.

## Fallback and Error Handling

The shared display-label rule must be resilient.

### Missing preferred label properties

If `Name`, `Title`, and `Email` are all absent or blank, render the primary key value.

### Missing related row

If a foreign key value exists but the related row cannot be found, render the raw FK value rather than failing or leaving the cell blank.

### Null FK

If the FK value is null, render an empty value.

### Non-FK scalar values

Non-relationship fields should continue using the normal scalar formatting path.

## Testing Strategy

### Core / renderer tests

Add or update tests to verify:

- themed index page markup includes stylesheet and semantic classes
- themed list page markup includes stylesheet and semantic classes
- list pages render prepared display values correctly
- existing form theming remains intact

### Integration / endpoint tests

Add or update tests to verify:

- top-level index page uses the shared theme
- entity list pages use the shared theme
- list pages show related labels instead of raw FK values where applicable
- forms still show the same human-readable related labels
- one-to-many ownership text still uses the shared label logic

### Fallback tests

Cover cases where:

- only `Title` exists
- only `Email` exists
- no preferred label property exists
- a related row referenced by FK is missing
- a nullable FK has no value

## Success Criteria

This design is successful when:

- index, list, and form pages look like one coherent UI
- readonly tables no longer expose raw FK values when a better identifying label is available
- all object-display paths use the same shared convention
- the convention remains simple and configuration-free for now
- the implementation stays server-rendered and lightweight
- tests prove both the happy path and fallback behavior
