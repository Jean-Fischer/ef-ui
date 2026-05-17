# Forms Chip-Picker Design

**Date:** 2026-05-17

## Objective

Improve the EF UI form experience with a focused first-pass polish that keeps the current server-rendered architecture intact.

This phase is limited to form UX, with the primary improvement being the many-to-many selector. The goal is to replace the current checkbox-heavy interaction with a more standard chip/token picker while keeping JavaScript minimal, local, and framework-free.

## Validated Decisions

- Scope the refresh to **forms only** for now.
- Do **not** adopt Tailwind itself in this phase.
- Use a **Tailwind-inspired local stylesheet** served by ASP.NET.
- Keep the UI **server-rendered**.
- Use JavaScript only where it produces a meaningfully better UX.
- Replace the current many-to-many checkbox picker with a **chip/token picker**.
- JavaScript support is acceptable and does not need full no-JS parity.
- Keep the JavaScript **small, clean, and self-contained**.
- Preserve the current backend contract based on repeated submitted values.

## Design Direction

The recommended direction is a **clean admin** visual style with semantic CSS classes rather than utility-first Tailwind classes.

The renderer should stop relying on inline form styling and instead emit reusable classes such as:

- `efui-form`
- `efui-field`
- `efui-label`
- `efui-input`
- `efui-select`
- `efui-button`
- `efui-help`
- `efui-error`
- `efui-chip-picker`

A single local stylesheet should define:

- neutral grays
- subtle borders
- modest radius
- compact, consistent spacing
- clear hover and focus states
- readable error styling

This provides most of the practical benefits of Tailwind-style discipline without adding a build step or Node dependency.

## Scope

This first pass updates only the generated **form experience**.

Included:
- form layout and spacing
- label and field styling
- input and select styling
- error presentation
- button styling
- many-to-many picker UX

Explicitly excluded for now:
- list pages
- tables
- navigation redesign
- admin layout overhaul
- advanced theming system

## Recommended Many-to-Many Component

The many-to-many field should become a **chip-picker** with three visible regions:

1. field label and helper/error area
2. selected chip area with removable chips
3. searchable results panel for unselected options

### Interaction Model

- Selected items appear as chips.
- Each chip has a small remove button.
- A search input filters the remaining available options.
- Clicking an available option adds it.
- Clicking a chip remove button removes it.
- Selected items are not shown again in the available results list.

This gives a more standard and understandable UX than a large checkbox wall while remaining much simpler than a full autocomplete widget.

## Rendering and Data Contract

The server remains responsible for rendering the page and the full option set.

The form submission contract stays unchanged: selected values are posted as repeated field values.

For example, for a field named `Tracks`, the expected POST shape remains:

```text
Tracks=2
Tracks=7
Tracks=11
```

This means:
- hidden repeated inputs should mirror the selected key set
- when nothing is selected, no value is submitted for that field
- the existing backend relationship update semantics can remain intact

## Markup Shape

The collection field renderer in `src/EfUi.Core/Rendering/HtmlPageRenderer.cs` should emit a semantic component shell that can be enhanced by a small local script.

Suggested structure:

- outer field container
- label
- selected-chip region
- search input
- available-results panel
- hidden-input host
- fallback markup or data source for initialization

The server should render the complete option list into the page so the client can enhance it without additional requests.

## JavaScript Strategy

JavaScript should be intentionally minimal and self-contained.

Responsibilities:
- initialize from server-rendered selected options
- maintain the selected key set
- render chips
- filter unselected options using case-insensitive substring matching
- add selections
- remove selections
- synchronize hidden repeated inputs before submit and during interaction

Not included in phase 1:
- async search
- remote loading
- virtualization
- free-text tag creation
- advanced combobox keyboard model
- framework dependency

## Fallback Strategy

Full non-JavaScript parity is not required.

The preferred fallback is simple and reliable rather than equivalent. Without JavaScript, the field may degrade to a plain checkbox list or another basic control that still allows relationship editing. With JavaScript enabled, that structure is enhanced into the chip-picker interaction.

This keeps the implementation resilient without complicating the design around no-JS perfection.

## State Rules

To keep the picker predictable:

- adding an already selected item does nothing
- removing a chip immediately removes the corresponding hidden input
- selected items never appear in the available results list
- filtering only affects the available results list
- clearing the search restores the full remaining option list
- no selected values means clearing the relationship set

The authoritative client-side state is the selected key set mirrored into hidden repeated inputs.

## Error Handling

If the server rejects submitted values or other validation fails:

- the form should re-render with submitted selections preserved
- error messages should use the new semantic form styling
- the picker should restore the submitted chip state from server-rendered values

If enhancement JavaScript fails to initialize, the fallback markup must still allow the user to complete the edit.

## Testing Strategy

### Renderer tests

Add or update tests to verify:
- semantic form classes are rendered for form fields
- collection fields render the chip-picker shell
- hidden-input hosting exists for repeated values
- selected items are represented correctly
- fallback markup remains available

### Endpoint tests

Verify real many-to-many workflows still behave correctly:
- add multiple related rows
- remove individual related rows
- clear the full related set
- preserve submitted values on validation rerender

### Manual verification

Use a realistic screen such as Chinook playlist editing and confirm:
- chips render for selected items
- searching filters remaining options
- adding/removing items updates visible state immediately
- form submission persists changes correctly
- validation errors preserve the picker state

## Success Criteria

This design is successful when:

- generated forms look cleaner and more consistent
- the many-to-many field no longer feels like a checkbox wall
- selected relationships are always visible as chips
- users can search and add options with minimal friction
- the backend submission contract remains unchanged
- the implementation stays lightweight, local, and framework-free
- no broad rewrite of the UI architecture is required
