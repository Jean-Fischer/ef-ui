# Relationship Support Design

**Date:** 2026-05-17

## Objective

Improve EF UI relationship editing so supported one-to-many relationships are presented as clearly as many-to-many relationships, while keeping the implementation lightweight, server-rendered, and safe by default.

The design should preserve the current strengths of the system:
- metadata-driven form generation
- simple HTML controls
- explicit server-side validation
- no hidden reparenting side effects

It should also support the important non-skip-navigation case where a relationship is modeled through a concrete join entity with payload columns.

## Validated Decisions

- Keep dependent-side **many-to-one** editing as a dropdown.
- Keep supported **many-to-many skip navigations** as the existing filterable checkbox picker.
- Add support for principal-side **one-to-many** editing using the **same picker style** as many-to-many.
- For one-to-many, show **all candidate children**.
- Children already assigned to another parent should be **visible but greyed out / disabled**.
- Reuse the same related-entity display-label logic already used for relationship options instead of showing only raw FK values.
- For **required one-to-many** relationships, if a user removes a currently attached child without a valid reassignment target, **save should fail with a validation error**.
- For **join entities with payload** (for example `OrderLine`, `PlaylistTrack`), support the case simply by treating the join as a normal entity and showing only a **manage related rows link** from the parent page.
- Do not support nested payload editing inside the parent edit form in the first pass.
- Do not silently reparent rows from one parent to another.

## Relationship Modes

The current implementation already distinguishes:
- scalar fields
- reference relationships
- collection relationships for skip navigations

The next version should think in terms of four UI modes.

### 1. Many-to-one

Example: `Track -> MediaType`

- Render as a dropdown on the dependent side.
- Bind through the existing FK scalar property.
- Keep current persistence behavior.

### 2. Many-to-many skip navigation

Example: `Playlist -> Tracks`

- Render as the current filterable checkbox picker.
- Selected values map directly to collection membership.
- Keep current repeated-form-value contract.

### 3. One-to-many principal collection

Example: `Artist -> Albums`

- Render as a filterable checkbox picker that visually matches the many-to-many control.
- Show all candidate child rows.
- Children currently assigned to this parent are checked.
- Children with no current parent are enabled and unchecked.
- Children assigned to another parent are visible but disabled / greyed out.
- Labels should explain disabled state, for example: `Album 3 (assigned to Artist Queen)`.

### 4. Join entity with payload

Example: `PlaylistTrack` with extra columns

- Do not render as a many-to-many picker.
- Treat the join entity as a normal editable entity.
- From the parent page, show only a **Manage related rows** link.
- If possible, link into a filtered list of the related entity.

## Evaluated Approaches

### Option A — Extend the current model with explicit relationship modes (**recommended**)

Keep the existing simple controls and add one new supported collection mode for one-to-many while handling join entities with payload as normal entities.

**Pros**
- minimal conceptual change
- reuses the current relationship-option label logic
- avoids unsafe automatic reparenting
- keeps the form engine understandable
- supports payload joins without pretending they are simple many-to-many

**Cons**
- introduces more metadata distinctions
- one-to-many persistence rules differ from many-to-many despite a similar UI

### Option B — Treat every collection relationship as the same picker

Use the same collection concept for skip navigations, one-to-many, and payload joins.

**Pros**
- superficially simple API
- maximizes UI reuse

**Cons**
- semantics become muddled
- payload joins do not fit cleanly
- high risk of incorrect save behavior
- difficult to explain and test

### Option C — Full nested subforms for collections

Allow add/edit/remove of child rows directly inside the parent form.

**Pros**
- most powerful long-term UX
- would cover payload joins eventually

**Cons**
- much more complex renderer and binding model
- much higher validation and testing burden
- unnecessary for the current goals

## Recommended Metadata Shape

The metadata layer should stop treating all collection relationships as equivalent.

A practical shape is to distinguish at least these field categories conceptually:
- scalar
- reference
- collection-many-to-many
- collection-one-to-many
- related-management-link

This can be implemented either by adding new enum values or by keeping the current enum and adding more relationship metadata. The important part is that rendering and persistence can tell the difference.

For one-to-many support, metadata must capture enough information to:
- identify the principal-side collection navigation
- identify the child CLR type
- identify the child FK property that points back to the parent
- determine whether the child FK is nullable

For many-to-many support, metadata can continue using the current skip-navigation detection.

For payload joins, detection should be conservative:
- if EF exposes the relationship as a concrete entity type rather than a skip navigation, do not inline it as many-to-many
- instead expose a related-management affordance

## Recommended Option Model

The current `RelatedEntityOption` concept is close to what is needed but likely needs small expansion.

Today it effectively carries:
- value
- label
- selected

For one-to-many, we likely also need:
- `Disabled`
- optional `Description` or `Reason`

That lets the renderer show states such as:
- selected for this parent
- available and unassigned
- unavailable because assigned elsewhere

The display-label heuristic can remain shared across many-to-one, many-to-many, and one-to-many. The current preference order of friendly text over raw key is still the right default.

## Rendering Model

### Many-to-one

Keep the current dropdown rendering.

No visual behavior change is required beyond continuing to use friendly labels.

### Many-to-many

Keep the current filterable checkbox picker.

### One-to-many

Render the same picker shell used by many-to-many:
1. field label
2. filter input
3. scrollable list of options

Option states:
- **checked + enabled**: child belongs to this parent
- **unchecked + enabled**: child is currently unassigned and can be attached
- **unchecked + disabled**: child belongs to another parent and is visible only for awareness

The filter should continue to keep selected items visible even when they do not match the active search query.

Disabled options should remain visible when they match the filter text. Their labels should communicate why they are unavailable.

### Join entity with payload

On the parent edit page, show a simple related-action area such as:

```html
<div>
  <label>PlaylistTracks</label>
  <a href="/efui/playlisttracks?playlistId=1">Manage related rows</a>
</div>
```

The first version does not need an embedded table, inline add form, or nested payload editor.

## Save Behavior

### Many-to-one

No change:
- posted selected value binds to the FK scalar property
- EF persists the relationship through the existing reference field handling

### Many-to-many

No change:
- repeated submitted values represent the selected set
- selected entities replace the current collection contents

### One-to-many

For a one-to-many picker, the submitted values represent the set of child rows that should belong to the current parent after save.

On load, the server computes option state by reading all candidate child rows and classifying each row as:
- currently attached to this parent
- unassigned
- assigned to another parent

On submit:
- newly selected unassigned children get their child FK set to the current parent key
- children still selected remain unchanged
- children that were attached to this parent but are now unselected are treated as removals
- disabled rows are rejected if they appear in submitted data

Removal handling:
- **optional FK**: set the child FK to `null`
- **required FK**: reject save with a field-level validation error

### Join entity with payload

No collection mutation happens from the parent page.

Users must manage payload join rows through the join entity screen itself.

## Validation and Error Handling

The server must enforce relationship state and never trust HTML disabled attributes alone.

### Required validations

- if a disabled one-to-many child is submitted, reject the save
- if a selected related row no longer exists, reject the save
- if a required one-to-many child is removed without reassignment, reject the save
- if metadata cannot identify a supported relationship shape safely, do not render inline editing for it

### Example error messages

- `Albums: Album \"News of the World\" is already assigned to another Artist.`
- `Tracks: Track \"Intro\" cannot be removed because PlaylistId is required.`
- `PlaylistTracks: manage related rows separately because this relationship contains payload columns.`

## Supported and Deferred Cases

### Supported in this design

- dependent-side many-to-one dropdowns
- skip-navigation many-to-many picker
- principal-side one-to-many picker with disabled externally assigned rows
- required one-to-many validation on removal
- join entity with payload as normal entity plus manage link

### Deferred / out of scope for first pass

- nested editing of join-entity payload rows
- automatic reparenting between parents
- composite-key related entities
- ambiguous multiple relationships between the same two CLR types unless metadata can disambiguate them safely
- full one-to-one editing UX

## Testing Strategy

### Metadata tests

Add tests that prove the provider can distinguish:
- reference many-to-one fields
- skip-navigation many-to-many fields
- principal-side one-to-many fields
- concrete join entities that should not be treated as skip-navigation many-to-many

### Renderer tests

Add tests that prove:
- one-to-many renders the same picker shell as many-to-many
- selected child rows are checked
- unassigned child rows are enabled
- rows assigned elsewhere are visible and disabled
- disabled labels include ownership context
- payload joins render a manage link instead of inline picker controls

### CRUD / persistence tests

Add tests that prove:
- selecting an unassigned one-to-many child assigns its FK to the current parent
- unselecting an optional child clears the FK
- unselecting a required child yields a validation error
- tampered submissions cannot attach a disabled child owned by another parent
- existing many-to-many semantics still work unchanged

### ASP.NET endpoint tests

Use sample data or Chinook-style entities to verify:
- the one-to-many picker appears for supported collection navigations
- disabled rows are visible in HTML
- invalid disabled submissions are rejected server-side
- optional and required one-to-many cases both behave as designed
- join entities with payload expose only a manage link from the parent page

## Success Criteria

The design is successful when all of the following are true:

- one-to-many relationships no longer force users to reason only in terms of raw FK values
- many-to-many and one-to-many share a familiar picker UI where appropriate
- users can see unavailable child rows without accidentally stealing them from another parent
- required one-to-many removals fail clearly and safely
- join entities with payload are supported simply and honestly through normal entity management
- the implementation remains metadata-driven, lightweight, and testable
