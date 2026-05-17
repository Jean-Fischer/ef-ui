# Many-to-Many Filterable Picker Design

**Date:** 2026-05-17

## Objective

Improve the many-to-many editing experience in EF UI by replacing the current native multi-select control with a more understandable component that supports a lightweight client-side text filter.

The first version should remain simple, keep server-rendered HTML as the default, require only tiny browser-side JavaScript, and preserve the existing relationship update semantics.

## Validated Decisions

- Replace the current many-to-many native `<select multiple>` with a **filterable checkbox picker**.
- Keep the control **compact**, with a scrollable box showing roughly **6 visible rows** by default.
- Reuse the **same related-entity label text** already generated today.
- Add a **client-only case-insensitive `contains` filter** above the checkbox list.
- Ensure **selected items remain visible** even when they do not match the active filter text.
- Treat **no checked boxes** as **clear all relationships**.
- Keep the persistence model the same: checkbox choices map to a set of selected related keys.
- Do not add advanced behaviors yet:
  - no autocomplete
  - no async search
  - no grouping
  - no virtualization
  - no framework dependency

## Evaluated Approaches

### Option A — Plain checkbox list

Replace the multi-select with an unfiltered checkbox list.

**Pros**
- obvious interaction model
- no JavaScript required
- easy to test

**Cons**
- becomes unwieldy for larger related sets
- poor experience for Chinook track lists or similar real data

### Option B — Filterable checkbox picker (**recommended**)

Render a search field plus a scrollable checkbox list and hide non-matching unselected items in the browser.

**Pros**
- still easy to understand
- solves the “too many items” problem better than a plain checkbox list
- requires only tiny JavaScript
- keeps progressive enhancement possible

**Cons**
- slightly more UI logic to maintain
- requires careful treatment of selected items during filtering

### Option C — Tag picker / autocomplete widget

Use a more advanced searchable selection component.

**Pros**
- best eventual UX
- scales better to very large lists

**Cons**
- more JavaScript/state complexity
- more testing burden
- overkill for the current needs

## Recommended UI Shape

For each supported many-to-many field, render:

1. field label
2. search input
3. scrollable bordered list of checkbox options

Each option row should contain:
- a checkbox
- a visible text label using the existing option-label heuristic

### Visual Characteristics

- compact default height
- approximately 6 visible rows
- vertical scrolling
- simple border and padding
- no special styling system required

## Filtering Behavior

The filter should be intentionally minimal:

- case-insensitive
- substring (`contains`) matching only
- browser-side only
- updates while the user types

### Visibility Rules

For each option:
- if the option is **checked**, it stays visible regardless of the filter text
- if the option is **unchecked**, it is visible only when its label contains the search text

This rule prevents selected relationships from disappearing during filtering, which would otherwise make the control confusing and risky to edit.

## Posting and Binding Contract

The server should continue to think in terms of selected related keys, but the web form contract should support repeated values naturally.

Expected POST shape for a collection field such as `Tracks`:

```text
Tracks=2
Tracks=3
Tracks=7
```

### Important Semantics

- repeated field names represent a selected set
- unchecked boxes produce no submitted value
- if the collection field is absent from the submitted form, that means **empty selection** for that rendered many-to-many field

This is necessary so users can remove all relationships, not just add or swap them.

## Implementation Shape

The server should render the full option list as normal HTML. A small piece of inline or local JavaScript should enhance the list with filtering.

### Suggested Markup Shape

```html
<label>Tracks</label>
<input type="search" placeholder="Filter..." />
<div class="efui-checkbox-picker">
  <label><input type="checkbox" name="Tracks" value="2" checked /> Track A</label>
  <label><input type="checkbox" name="Tracks" value="3" /> Track B</label>
</div>
```

### Suggested Client-Side Logic

On each `input` event from the search box:
1. normalize the query to lowercase
2. inspect each checkbox row
3. keep it visible if checked
4. otherwise show only if the label text contains the query

No debounce is required in the first version.

## Progressive Enhancement

Without JavaScript:
- the checkbox list still works correctly
- filtering is simply unavailable

With JavaScript:
- filtering improves usability
- form submission remains standard HTML

This keeps the feature resilient and easy to reason about.

## Testing Strategy

### Renderer tests

Add or update tests to prove that supported many-to-many fields render:
- a search input
- a scrollable boxed list
- checkbox inputs instead of `<select multiple>`
- the same labels used today
- checked items rendered as checked

### CRUD / binding tests

Ensure the many-to-many update path consumes repeated field values rather than a comma-separated string and still supports:
- adding multiple related rows
- removing some related rows
- clearing the entire selection

### ASP.NET endpoint tests

Use Chinook `Playlist -> Tracks` as the real-world proof:
- `/chinook/playlists/1/edit` renders the filterable checkbox picker
- posting repeated `Tracks` values updates the playlist
- posting no `Tracks` values clears the set

### Manual verification

Open the Chinook playlist editor and verify:
- typing in the filter narrows visible unchecked items
- checked items stay visible
- checking/unchecking works without keyboard tricks

## Success Criteria

The design is successful when all of the following are true:

- the native multi-select is gone for supported many-to-many fields
- the replacement control is obviously editable without special browser knowledge
- users can filter large option lists with simple text search
- selected items never disappear during filtering
- clearing all relationships is possible
- the implementation remains lightweight and framework-free
