# Tabulator Table Refresh Design

## Goal

Make list pages feel like a single, stable grid surface instead of a page that visibly reloads when the user filters or sorts. The table should refresh in place, keep the browser on the same document, and rely on Tabulator for the visible interaction model as much as possible.

## Problem Statement

The current list experience still flashes like a full page refresh when filters are applied. That makes the UI feel heavier than it should and breaks the sense that the user is working inside a data grid.

The current design also exposes too much non-grid chrome for list pages. The query-builder bar is redundant once the table can own sorting and filtering interactions directly.

## Validated Decisions

- Remove the query-builder UI from list pages.
- Make the table the only visible list interaction surface.
- Use Tabulator header filters and sortable headers wherever they fit the backend model.
- Apply filters explicitly, rather than refreshing on every keystroke.
- Refresh rows in place instead of navigating or re-rendering the full document.
- Keep the server authoritative for query semantics, validation, and returned rows.
- Preserve the current URL/query contract so list state remains shareable and back/forward friendly.
- Keep the HTML fallback table for resilience when enhancement is unavailable.

## Recommended Approach

Use Tabulator as the client-side interaction shell, but keep the server as the source of truth for filtering and sorting.

In practice, that means:

1. The page renders a server-authored list shell with breadcrumbs, title, actions, and fallback table.
2. Tabulator initializes over the enhancement host.
3. Column headers expose sorting and filtering controls.
4. When the user applies a filter or sort, the client reads Tabulator state.
5. The client converts that state into the existing backend query shape.
6. The client fetches the JSON data endpoint instead of navigating the browser.
7. The grid updates its rows in place.
8. The address bar is kept in sync so the state remains linkable.

This keeps the backend structure largely intact while removing the page-blink effect.

## Why This Fits the Current Backend

The backend already understands a canonical list query contract built from:

- `filter.N.field`
- `filter.N.op`
- `filter.N.value`
- `sort.N.field`
- `sort.N.dir`
- `offset`
- `limit`

That is a good boundary. The client should not invent a second query language. Instead, it should translate Tabulator state into that same contract and let the server continue to own binding, validation, and result shaping.

This minimizes change risk because:

- existing query semantics remain unchanged
- related-row prefilters can continue to work
- FK label/value rules remain server-defined
- the fallback HTML table can remain untouched
- future paging or virtualization can build on the same endpoint

## Page Structure

List pages should keep a compact layout:

1. breadcrumb navigation
2. page title
3. primary actions such as `Create New`
4. enhancement host for Tabulator
5. fallback HTML table
6. compact status/error region

The query-builder bar should not appear at all.

## Client Interaction Model

Tabulator should own the visible grid controls.

### Sorting

- Sortable columns use Tabulator header sorting.
- The client reads Tabulator sort state when the user changes sort.
- The request is sent to the JSON endpoint using the canonical sort parameters.
- Synthetic actions columns must not be sortable.

### Filtering

- Filterable columns use Tabulator header filters.
- The filter flow should be explicit: the table refreshes when the user applies the filter, not on every keypress.
- The client should only expose filter operators that map cleanly to the current backend model.
- Synthetic actions columns must not be filterable.

### Refresh Behavior

- The table must remain mounted.
- Only the data changes.
- No `window.location.assign(...)` style reloads for normal filter/sort changes.
- The URL should still be updated with the current query state.
- Back/forward navigation should rehydrate the current grid state from the URL.

## Data Flow

### Initial load

1. Server renders the page shell.
2. Server emits the initial enhancement config and fallback table.
3. Tabulator initializes from the embedded data.
4. The fallback table stays available until enhancement is confirmed.

### Filter or sort apply

1. User applies a filter or changes sort.
2. Client reads live Tabulator state.
3. Client serializes that state into the existing query contract.
4. Client requests the JSON data endpoint.
5. Server validates and executes the query.
6. Server returns authoritative rows and normalized query/status data.
7. Client updates Tabulator rows in place.
8. Client updates status UI and the browser URL.

### Back/forward

On `popstate`, the client should re-read the URL, fetch the matching JSON payload, and restore the visible grid state from the authoritative response.

## Loading UX

The loading state should feel attached to the grid, not like a page transition.

Preferred behavior:

- show a small loading indicator inside or directly above the table area
- keep the table structure visible
- do not blank the page
- do not remount the whole document
- use Tabulator’s own loading mechanisms if they fit cleanly

The goal is to show progress without creating a second page-level chrome stack.

## Error Handling

If the server rejects the query or returns validation issues:

- keep the page shell intact
- preserve the grid surface
- surface the problem in the compact table status region
- avoid a full reload loop

If the JSON request fails unexpectedly:

- keep the last visible rows if possible
- show a local error state
- do not destroy the current table instance unless enhancement must be reset

## Fallback Behavior

The fallback HTML table remains important for resilience.

If Tabulator fails to initialize:

- the fallback table should still render
- links and row actions should still work
- the page should remain usable

In normal use, the fallback should be hidden once the enhancement is active.

## Accessibility Notes

Because the page is becoming more grid-like, the client should preserve useful keyboard and screen-reader behavior:

- header filters must remain reachable
- loading feedback should be announced appropriately
- validation and error messages should be visible and readable
- the fallback table should remain semantic HTML

## Options Considered

### Option A — Keep the query-builder and reload navigation

This is the smallest change, but it preserves the blink and keeps the page feeling heavier than necessary.

### Option B — Tabulator-driven in-place refresh with server-backed JSON data (**recommended**)

This keeps the backend contract, removes the blink, and uses Tabulator where it fits best.

### Option C — Fully client-owned table state and query semantics

This would be more flexible, but it is too large a shift away from the current server-first structure.

## Testing Strategy

Cover the new behavior at three levels:

### Renderer tests

- the query-builder is no longer rendered
- the list page still renders breadcrumbs and primary actions
- the enhancement config still carries the table metadata needed by Tabulator
- fallback HTML table rendering still works

### Endpoint tests

- filter and sort requests still bind to the canonical query contract
- the JSON data endpoint returns authoritative rows
- invalid query combinations are reported cleanly
- related-row prefilters still round-trip correctly

### Asset tests

- Tabulator headers drive sort/filter interactions
- filter apply triggers a data refresh without full navigation
- the loading state appears in the grid area
- the browser URL stays synchronized with table state

## Non-Goals

- full SPA conversion
- client-side ownership of EF query semantics
- arbitrary advanced filter composition beyond what maps cleanly to the current backend
- removing the fallback HTML-first path

## Implementation Note

This change should be made incrementally so the current server contract stays stable while the client interaction layer is simplified.
