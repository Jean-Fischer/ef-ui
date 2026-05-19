# JSON-Backed Table Data Design

## Goal

Move Tabulator list interactions from full-page navigation to a JSON data flow so filtering, sorting, and future paging/virtualization can update the grid without page blinking, while keeping the server authoritative for query semantics.

## Problem Statement

The current list enhancement uses Tabulator for the visible UI, but every filter or sort interaction still turns into `window.location.assign(...)`. That creates a full document reload on each change. The result is visible blink, fragile state hydration, and a poor fit for future remote paging/windowed loading.

## Constraints

- Preserve the existing server-rendered HTML page shell.
- Keep the current URL/query contract: `filter.N.field`, `filter.N.op`, `filter.N.value`, `sort.N.field`, `sort.N.dir`, `offset`, `limit`.
- Keep the server authoritative for filtering, sorting, FK label semantics, validation, and related-row prefilters.
- Preserve the server-rendered fallback table when JS enhancement is unavailable.
- Keep FK cells linking to related edit pages and keep row actions.
- Keep the compact list status area visible and in sync with active query state.
- Leave room for future virtual scrolling/windowed loading.

## Options Considered

### Option A — Keep full-page navigation and keep patching hydration

This keeps the current architecture and tries to make reload-driven UX feel better.

**Pros**
- Smallest code delta.
- No new endpoint shape.

**Cons**
- Blink is inherent because the whole page still reloads.
- Header-filter UX remains structurally awkward.
- Future remote paging/virtualization still fights the architecture.

### Option B — Hybrid page shell + JSON data endpoint (**recommended**)

Keep the page shell server-rendered, but move list data refreshes to a JSON endpoint, with URL updates done via `history.replaceState(...)` instead of full reloads.

**Pros**
- Removes blink.
- Preserves server authority and shareable URLs.
- Fits future remote paging/windowed loading.
- Retains HTML fallback.

**Cons**
- Requires a new endpoint and client-side state synchronization.
- The compact status strip must be updated client-side after fetches.

### Option C — Fully client-owned list page

Make the whole list page client-rendered.

**Pros**
- Maximum flexibility.

**Cons**
- Larger architecture change.
- Moves too far away from the current server-first direction.
- Weakens the existing HTML fallback story.

## Recommended Architecture

Adopt **Option B**.

Each list page continues to render:
- breadcrumbs
- page title and create action
- compact status container
- enhancement shell + embedded initial config
- fallback HTML table

Add a JSON endpoint per entity list:
- `GET /<mount>/<entity>/data`

That endpoint will reuse the same bound query contract and server-side list rendering pipeline as the HTML page, but return JSON instead of a full document.

## Data Flow

### Initial page load

1. Server renders the list page HTML.
2. Embedded config contains:
   - `listUrl`
   - `dataUrl`
   - column metadata
   - initial rows
   - initial query state
   - initial status model
3. Tabulator boots from the embedded rows.
4. Fallback HTML table stays available until enhancement is ready.

### Filter/sort interaction

1. User edits a header filter or clicks a sortable header.
2. Client reads live Tabulator state.
3. Client translates that state into the existing URL contract.
4. Client updates the browser URL with `history.replaceState(...)`.
5. Client requests `dataUrl + '?' + params`.
6. Server validates and executes the query.
7. Server returns JSON with rows + normalized query/status/errors.
8. Client updates:
   - Tabulator rows
   - compact status UI
   - loading state
9. No full-page reload occurs.

### Back/forward support

On `popstate`, the client re-reads `window.location.search`, fetches fresh JSON, and rehydrates Tabulator’s visible header filters/sorts from the authoritative server response.

## JSON Contract

Return a compact payload shaped for the current enhancement shell:

```json
{
  "listUrl": "/chinook/tracks",
  "dataUrl": "/chinook/tracks/data",
  "columns": [
    {
      "field": "MediaTypeId",
      "title": "MediaTypeId",
      "headerSort": true,
      "headerFilter": "input",
      "filterOperator": "eq",
      "activeFilterOperator": "eq",
      "headerFilterValue": "1",
      "sortDirection": null,
      "isFilterable": true
    }
  ],
  "rows": [
    {
      "Name": { "text": "Track A", "href": null },
      "MediaTypeId": { "text": "MPEG audio file", "href": "/chinook/media_types/1/edit" },
      "__actions": "<a ...>Edit</a>..."
    }
  ],
  "query": {
    "filters": [{ "field": "MediaTypeId", "operator": "eq", "value": "1" }],
    "sorts": [],
    "offset": 0,
    "limit": 25
  },
  "status": {
    "items": ["MediaTypeId eq 1"],
    "errors": [],
    "emptyMessage": null,
    "offset": 0,
    "limit": 25
  }
}
```

The first implementation can keep rows fully materialized in JSON and defer total-count/virtual-scroll metadata until the next step.

## Server Responsibilities

The server remains authoritative for:
- query binding and validation
- allowed filter/sort fields
- FK label-based filtering/sorting semantics
- pagination semantics
- cell/link/action rendering shape
- compact status contents and error messages

The new JSON endpoint should reuse the existing list query pipeline instead of introducing a separate interpretation path.

## Client Responsibilities

The client enhancement becomes responsible for:
- reading live Tabulator filter/sort state
- building the existing URL query contract
- calling the JSON endpoint
- updating Tabulator data without reloading the page
- updating the compact status DOM
- syncing the address bar with `replaceState`
- responding to `popstate`
- preserving loading feedback and fallback hiding behavior

## Error Handling

If the JSON request returns query validation errors:
- keep the page shell intact
- update the status/error area from the JSON payload
- render the authoritative rows returned by the server
- do not full-reload the page

If the request fails unexpectedly:
- keep the existing visible grid data
- show an in-grid/local loading error state
- do not destroy the current table instance

## Testing Strategy

Add coverage for:
- new `/data` endpoint shape and query-state reflection
- list page config containing `dataUrl`
- client asset using `fetch`/`replaceState` rather than `window.location.assign(...)`
- no remaining reload-driven filter flow in the enhancement asset
- related-row prefilters still arriving as visible editable state
- FK eq-related filters still preserving `eq` semantics when edited
- URL/state/status synchronization across filter/sort changes

## Migration Notes

This change is intentionally incremental:
- keep the HTML list page contract
- add JSON list data beside it
- keep the fallback table
- move only the enhanced path to remote JSON refreshes

That gives us a clean upgrade path toward remote paging and future virtual scrolling without forcing a full SPA rewrite.
