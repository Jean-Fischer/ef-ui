# Plan 001: Render type-aware native controls for scalar edit fields

> **Executor instructions**: Follow this plan step by step. Run every verification command and confirm the expected result before moving to the next step. If anything in the "STOP conditions" section occurs, stop and report — do not improvise. When done, update the status row for this plan in `plans/README.md` — unless a reviewer dispatched you and told you they maintain the index.
>
> **Drift check (run first)**: `git diff --stat deff891..HEAD -- src/EfUi.Core/Rendering/HtmlPageRenderer.cs tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs src/EfUi.AspNetCore/README.md`
> If any in-scope file changed since this plan was written, compare the "Current state" excerpts against the live code before proceeding; on a mismatch, treat it as a STOP condition.

## Status

- **Priority**: P2
- **Effort**: M
- **Risk**: MED
- **Depends on**: none
- **Category**: direction
- **Planned at**: commit `deff891`, 2026-06-20
- **Issue**: _not published_

## Why this matters

EF UI already knows which scalar CLR types are supported, but the edit/create forms still render every scalar as the same plain text `<input>`. That makes booleans, datetimes, enums, and numeric fields harder to use than they need to be and forces users to type the serialized value shape by hand. The repo already has a stronger type surface in metadata and binder code, plus an intent doc that explicitly calls for type-specific HTML controls. This plan turns that latent knowledge into the form UI without changing the CRUD contract.

## Current state

The executor needs these facts inlined, not implied:

- `src/EfUi.Core/Rendering/HtmlPageRenderer.cs` — the form renderer; `RenderEditableField(...)` dispatches reference/collection/scalar, and `RenderScalarField(...)` currently emits one generic text input for every scalar.
  - `HtmlPageRenderer.cs:302-352` includes the current scalar branch and the single render line:
    `html.Append($"<input class=\"efui-input\" name=\"{field.Name}\" value=\"{WebUtility.HtmlEncode(value)}\" />");`
- `src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs` — the discovery layer already identifies the supported scalar set.
  - `EfEntityMetadataProvider.cs:378-392` currently treats `string`, numeric types, `bool`, `DateTime`, `Guid`, and enums as supported scalars.
- `src/EfUi.Core/Binding/ScalarValueBinder.cs` — the POST binder already parses the same supported types.
  - `ScalarValueBinder.cs:7-34` already handles `bool.Parse(...)`, `DateTime.Parse(...)`, nullable blank values, and enums.
- `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs` — the simple-host integration tests currently prove the existing generic control behavior.
  - `EfUiEndpointsTests.cs:555-724` currently asserts `name="IsActive" value="True"`, `name="IsActive" value="False"`, and `name="CreatedAt" value="2026-05-18T12:30:00.0000000"` / `bad-date` as raw text inputs.
- `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs` — the renderer test suite already has a good pattern for preserving submitted values on validation failure.
  - `HtmlPageRendererTests.cs:666-704` is the existing `CreatedAt` preservation test to extend or mirror.
- `src/EfUi.AspNetCore/README.md` — the package docs still describe generic scalar support rather than type-specific controls.
  - `src/EfUi.AspNetCore/README.md:75-83` says the editor supports common scalar CLR types such as `string`, numeric types, `bool`, `DateTime`, `Guid`, and enums.
- `docs/plans/poc-design-doc.md` — the original design intent already called for type-specific HTML elements.
  - `docs/plans/poc-design-doc.md:221-223` says to use `<input>`, `<select>`, or other elements based on property type, with `DateTime → <input type="datetime-local">` as the example. For this plan, we are intentionally *not* adopting the `datetime-local` example; we are keeping `DateTime` as a text input with validation.

Repo conventions that apply here:

- Preserve the server-rendered HTML-first architecture. Existing form controls are produced in `HtmlPageRenderer`; do not introduce a client-side form framework.
- Follow the existing semantic CSS class pattern already in `HtmlPageRenderer` and `EfUiFormCss` (`efui-form`, `efui-field`, `efui-input`, `efui-select`).
- Keep the type mapping local to the renderer unless a new public abstraction clearly pays for itself; avoid widening the public surface just to encode control choice.
- Match the current test style: small focused xUnit tests with FluentAssertions, and endpoint tests that assert the actual rendered markup.

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Renderer characterization tests | `dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter "FullyQualifiedName~HtmlPageRendererTests"` | New expectations fail before the implementation, then pass after it |
| Endpoint form tests | `dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "FullyQualifiedName~EfUiEndpointsTests"` | New expectations fail before the implementation, then pass after it |
| Full build | `dotnet build EfUi.sln -c Release` | exit 0 |
| Full test run | `dotnet test EfUi.sln -c Release --no-build` | exit 0 |

## Suggested executor toolkit

- No special skill is required beyond the repo's normal .NET test workflow.
- If a browser check is available, manually load the sample host and inspect one create/edit form after the automated tests pass; this is optional, not the primary verification gate.

## Scope

**In scope** — the only files you should modify:

- `src/EfUi.Core/Rendering/HtmlPageRenderer.cs`
- `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`
- `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- `src/EfUi.AspNetCore/README.md`

**Out of scope** — do NOT touch, even though they look related:

- `src/EfUi.Core/Binding/ScalarValueBinder.cs` — the current binder already parses the supported scalar set; do not change POST semantics unless a test proves you must.
- `src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs` — the supported scalar discovery set is already correct for this pass.
- Any relationship picker, one-to-many picker, list-page, Tabulator, or antiforgery code.
- `DateOnly` / `TimeOnly` support, timezone semantics, or a custom JavaScript date picker; those are separate scope decisions.
- `README.md` at the repo root unless the docs diff clearly demands it.

## Git workflow

- Match the repo's existing conventional commit style if you commit (`feat: ...` / `fix: ...`), see `git log --oneline`.
- Keep changes on the current branch or the designated worktree only; do not push or open a PR unless the operator explicitly asks.

## Steps

### Step 1: Lock the desired controls with failing tests

Add renderer-level tests that describe the target scalar-control mapping before you touch the implementation. Use `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs` as the main pattern, and mirror the existing submitted-value preservation style from `RenderEditForm_prefers_submitted_values_over_model_values`.

Add assertions for these cases at minimum:

- non-nullable `bool` renders as a checkbox-style control rather than a raw text box, with a server-friendly fallback value so unchecked posts still bind correctly
- nullable `bool?` renders a control that can express blank / `true` / `false` explicitly
- `DateTime` stays as a text input, but renders a clear format hint/validation contract using the ISO-8601 subset pattern `^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d{1,7})?)?(Z|[+-]\d{2}:\d{2})?$` and a placeholder like `2026-05-17T10:30:00Z` so users enter a consistent value
- one numeric scalar and one enum scalar render with native number/select semantics instead of the generic text input
- submitted values still win over model values on validation failure for the new controls

Then add or update one or two endpoint tests in `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs` so the `/simple/users/new` and `/simple/users/{id}/edit` pages prove the simple host uses the same native controls and still round-trips `IsActive` / `CreatedAt` correctly.

**Verify**: run the two targeted `dotnet test` commands above. **Expected**: the new assertions fail first, and the failure output points at the missing native-control behavior rather than unrelated regressions.

### Step 2: Implement a scalar-control mapper inside `HtmlPageRenderer`

Refactor `src/EfUi.Core/Rendering/HtmlPageRenderer.cs` so `RenderScalarField(...)` delegates to small private helpers that choose markup by `field.ValueType` and `field.IsRequired`.

Use this mapping as the intended first pass:

- `string` and `Guid` — keep a plain text input, but keep the existing CSS class and submitted-value precedence
- integral and decimal types — render `input type="number"`; use a sensible `step` value (`1` for integer types, `any` for floating/decimal types)
- `DateTime` — keep a text input, but add a lightweight validation contract using the ISO-8601 subset pattern `^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d{1,7})?)?(Z|[+-]\d{2}:\d{2})?$` and a placeholder like `2026-05-17T10:30:00Z`
- non-nullable `bool` — render a checkbox-style control with a hidden fallback value so unchecked posts still submit `false`
- nullable `bool?` — render a three-way select that can express blank / `true` / `false`
- enums — render a select populated from the enum values, with the current submitted/model value selected

Keep the following invariants intact:

- submitted values continue to override the model when redisplaying validation errors
- the field name posted back to the server stays exactly `field.Name`
- the existing reference and collection field rendering paths do not change
- the binder does not need a new contract for the supported scalar set

After the renderer refactor, rerun the targeted renderer and endpoint tests.

**Verify**: the same two targeted `dotnet test` commands. **Expected**: all added renderer and endpoint assertions pass, and the output shows the simple-host forms now contain the new native controls.

### Step 3: Update the package docs and run the release gates

Update `src/EfUi.AspNetCore/README.md` so the limitations section no longer implies every supported scalar is rendered as the same generic text box. Document the intended control family briefly: checkbox/select for booleans, text input with the ISO-8601 subset validation pattern above for `DateTime`, number inputs for numeric types, and select/input fallbacks for the rest of the supported scalar set.

Then run the repo's normal verification gates in Release mode.

**Verify**:

1. `dotnet build EfUi.sln -c Release` → exit 0
2. `dotnet test EfUi.sln -c Release --no-build` → exit 0

## Test plan

- Add or update renderer tests in `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs` for the new scalar-control mapping and submitted-value preservation.
- Add or update endpoint tests in `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs` for the simple host form pages and POST round-trips.
- Use the existing `RenderEditForm_prefers_submitted_values_over_model_values` test as the pattern for value preservation, and the current `/simple/users/...` endpoint tests as the integration pattern.
- Keep the tests focused on markup and POST behavior; do not add browser automation unless the HTML tests miss a real round-trip issue.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] The new renderer tests for bool / nullable bool / `DateTime` / numeric / enum controls exist and pass.
- [ ] The simple-host endpoint tests for `/simple/users/new` and `/simple/users/{id}/edit` pass with the new control expectations.
- [ ] `dotnet build EfUi.sln -c Release` exits 0.
- [ ] `dotnet test EfUi.sln -c Release --no-build` exits 0.
- [ ] `src/EfUi.AspNetCore/README.md` reflects the new scalar-control behavior.
- [ ] No files outside the in-scope list are modified (`git status`).
- [ ] `plans/README.md` status row is updated.

## STOP conditions

Stop and report back (do not improvise) if:

- The current `HtmlPageRenderer` code no longer matches the excerpts above; the codebase has drifted and the plan needs re-scoping.
- A native control choice would require changing the binder contract or inventing a new client-side workaround.
- the chosen `DateTime` validation contract cannot be expressed with the existing text-input binder flow or it forces a timezone policy decision that belongs in a different plan.
- Implementing the mapping would require touching relationship pickers, list rendering, antiforgery, or other out-of-scope areas.
- Any verification command fails twice after a reasonable fix attempt.

## Maintenance notes

- If `EfEntityMetadataProvider` later expands the supported scalar set, extend the scalar-control helper and its tests in lockstep; do not add another one-off branch in `RenderScalarField`.
- Reviewers should pay special attention to checkbox ordering, hidden fallback inputs, and the exact date-time validation pattern because those details determine whether POSTs bind correctly.
- If the team later decides to support `DateOnly`, `TimeOnly`, or a custom formatting attribute, that should become a separate plan rather than a surprise expansion inside this one.
