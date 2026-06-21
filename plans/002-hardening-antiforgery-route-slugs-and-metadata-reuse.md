# Plan 002: Harden antiforgery, route slugs, and metadata reuse

> **Executor instructions**: Follow this plan in order. Read the whole file before editing anything. Run every verification command and confirm the expected result before moving to the next step. If a STOP condition fires, stop and report back instead of improvising. When done, update the status row for this plan in `plans/README.md`.
>
> **Drift check (run first)**: `git diff --stat ae03cb8..HEAD -- src/EfUi.AspNetCore/EfUiRequestForgery.cs src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs src/EfUi.AspNetCore/README.md`
>
> If any in-scope file has changed since this plan was written, compare the "Current state" section against the live code before proceeding. On a mismatch, treat it as a STOP condition.

## Status

- **Priority**: P1
- **Effort**: L
- **Risk**: HIGH
- **Depends on**: none
- **Category**: security / correctness / performance
- **Planned at**: commit `ae03cb8`, 2026-06-21
- **Issue**: _not published_

## Why this matters

This branch closes three independent but high-value findings:

1. the current write-form protection only protects a random token and does not bind the token to the browser session, so it is replayable once learned;
2. route names are derived from the lowercased table name alone, so two mapped tables with the same name can collide;
3. metadata discovery and table-row loading are recomputed repeatedly, so the current request path does more work than necessary on every list render.

The goal of this plan is **not** to redesign the product. It is to harden the current server-rendered architecture with the smallest change set that fixes the real issues and keeps existing URLs and markup stable unless a collision truly requires a new route name.

## Current state

These are the facts the executor needs inlined:

- `src/EfUi.AspNetCore/EfUiRequestForgery.cs:17-49` currently generates a protected nonce and validates by unprotecting the posted token only. `HttpContext` is ignored in both methods, so there is no cookie/session pairing today.
- `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs:181-255` calls `EfUiRequestForgery.ValidateRequest(...)` on every POST route, and `:100-101`, `:131`, and `:168` mint tokens for the form pages.
- `src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs:353-357` computes route names as `tableName ?? clrType.Name`, lowercased. There is no collision registry, schema prefix, or other disambiguation.
- `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs:320-321` resolves entity routes with exact string equality against `metadata.RouteName`.
- `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs:495-563` rebuilds list rows and related lookups every request. `ReadRows(...)` reflects `DbContext.Set<TEntity>()` and materializes the entire set with `ToList()`; `BuildRelatedValueLookups(...)` re-reads related tables again for each foreign key.
- `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs:699-725` already has a small per-request related-row cache for form options, which is a good pattern to extend rather than replace.
- `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs:780-840` already contains a `TableSelectCountingInterceptor` and a `Get_duplicate_related_field_create_form_reads_customers_once()` test. That is the exact pattern to mirror for the list-page row-loading regression.
- `tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs` already has the shape of the metadata tests, including route-name assertions and special-case entity contexts.
- `src/EfUi.AspNetCore/README.md:72-84` still documents antiforgery only as hidden tokens and still lists the large-table in-memory limitation. If the security wording changes, this README should be updated to match the actual behavior.

Repo conventions that apply here:

- Keep the architecture server-rendered and package-owned. Do not introduce a client-side auth/token framework, MVC controllers, or a broad middleware rewrite just to solve these findings.
- Prefer small, focused xUnit tests with FluentAssertions. For request-count checks, reuse the `DbCommandInterceptor` style already present in the ASP.NET Core tests.
- Keep route naming changes minimal: preserve the current table-name-driven URLs for non-colliding entities; only disambiguate actual collisions.
- Treat discovery caches as model-scoped or request-scoped only. Do **not** introduce an app-global cache keyed by arbitrary strings unless a test proves you need it.

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Antiforgery regression tests | `dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "FullyQualifiedName~EfUiEndpointsTests"` | The new cookie/token pairing test fails before implementation, then passes after it |
| Route-name regression tests | `dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter "FullyQualifiedName~EntityMetadataProviderTests"` | The new collision-disambiguation test fails before implementation, then passes after it |
| Metadata cache / row-loading regression tests | `dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter "FullyQualifiedName~EntityMetadataProviderTests|FullyQualifiedName~HtmlPageRendererTests"` and `dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter "FullyQualifiedName~EfUiEndpointsTests"` | The new discovery-cache and list-page query-count tests fail before implementation, then pass after it |
| Full build | `dotnet build EfUi.sln -c Release` | exit 0 |
| Full test run | `dotnet test EfUi.sln -c Release --no-build` | exit 0 |

## Scope

**In scope** — the only files you should modify:

- `src/EfUi.AspNetCore/EfUiRequestForgery.cs`
- `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs`
- `src/EfUi.Core/Metadata/EfEntityMetadataProvider.cs`
- `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- `tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs`
- `src/EfUi.AspNetCore/README.md`
- `plans/README.md`

**Out of scope** — do NOT touch, even though they look related:

- `src/EfUi.Core/Crud/EntityCrudService.cs` — the write-path binder is not part of this fix.
- `src/EfUi.Core/Rendering/HtmlPageRenderer.cs` and `src/EfUi.Core/Rendering/RenderedListPayloadFactory.cs` — keep the rendering contract stable unless a test proves a follow-on change is needed.
- `src/EfUi.SampleHost/**` and `tests/EfUi.AspNetCore.Tests/Browser/**` — no browser automation changes are required for these findings.
- `.github/workflows/**`, `Directory.Build.props`, and package references — this is not a framework-version or CI change.
- Any broad move to MVC antiforgery middleware, controllers, or a new public authentication abstraction.

## Git workflow

- Keep the work on the current branch only; do not push or commit unless the operator explicitly asks.
- When you do update `plans/README.md`, use `git.exe` on this Windows setup.
- Keep changes small enough that each step can be reviewed against its test failure.

## Steps

### Step 1: Add the failing antiforgery regression first

The existing helper only protects a nonce; the fix should require a route-scoped cookie/request-token pair so the hidden form token cannot be replayed from a different browser session.

Add a new ASP.NET Core integration test in `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs` named something like:

- `Post_create_user_with_token_but_without_matching_antiforgery_cookie_is_rejected`

Test shape:

1. Use one client to GET `/simple/users/new` and capture the hidden `__RequestVerificationToken` value.
2. Use a second client that does **not** have the antiforgery cookie from the first client.
3. POST the form fields plus the captured hidden token.
4. Assert `StatusCode.BadRequest`.

That test should fail today because the helper only checks that the protected token can be unwrapped.

Then run:

```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter FullyQualifiedName~Post_create_user_with_token_but_without_matching_antiforgery_cookie_is_rejected
```

Expected: FAIL for the current implementation.

### Step 2: Implement the cookie-paired antiforgery helper

Strengthen `src/EfUi.AspNetCore/EfUiRequestForgery.cs` without introducing a broader framework dependency.

Target behavior:

- On GET/render paths, mint or reuse a **route-scoped antiforgery cookie** for the current mount point.
- Keep the cookie `HttpOnly`, `SameSite=Strict` or the closest safe equivalent that still works for this app, and scoped so it is only sent back to the same EF UI mount.
- Include the cookie secret plus the normalized route prefix in the protected request-token payload.
- On POST validation, require both the request token and the matching cookie. If the cookie is absent or does not match the token payload, return `false`.
- Preserve the current token hidden-field name so existing forms and tests keep working.

Update the GET call sites in `EfUiApplicationBuilderExtensions.cs` so they still obtain the token the same way, but now get the paired cookie as part of the same helper call.

**Do not** switch to MVC controllers or a whole new middleware pipeline. The fix should stay inside the existing package-owned helper and minimal API call sites.

Rerun the new antiforgery test.

Expected: PASS.

### Step 3: Add the route-name collision regression before changing the naming algorithm

Add metadata tests in `tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs` for a context that maps two different entity types to the same table name in different schemas.

Suggested test names:

- `GetEntities_disambiguates_colliding_table_names_from_different_schemas`
- `GetEntity_resolves_schema_scoped_routes_case_insensitively`

Suggested test shape:

- Create a small test `DbContext` with two entities mapped like `sales.users` and `audit.users` (or another clearly colliding pair).
- Assert that `GetEntities(...)` returns two distinct route names.
- Assert that each entity can still be resolved by its assigned route name.
- Keep the assertion focused on route uniqueness and resolution, not on incidental ordering.

This should fail against the current `GetRouteName(...)` implementation because it only lowercases the table name.

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter FullyQualifiedName~GetEntities_disambiguates_colliding_table_names_from_different_schemas
```

Expected: FAIL.

### Step 4: Implement route disambiguation with the smallest stable slug algorithm

Update `EfEntityMetadataProvider` so route names remain table-driven, but collisions cannot happen silently.

Intended algorithm:

1. Build a canonical base slug from the store object:
   - prefer `schema + '_' + table` when a schema exists
   - otherwise use the table name
   - fall back to `ClrType.Name` only when there is no table name
2. Normalize the slug consistently (the current lowercasing convention is fine).
3. Maintain a discovery-time set of already-used route names.
4. If a new slug collides, append a deterministic suffix derived from the entity identity that will stay stable for that mapped type. Prefer something readable over a hash if it can stay unique.
5. Make the route lookup in `EfUiApplicationBuilderExtensions` case-insensitive so the resolved URL does not depend on exact casing.

Keep the existing URLs for all non-colliding entities unchanged. Only the colliding cases should receive new names.

Rerun the route-name test.

Expected: PASS.

### Step 5: Add the discovery-cache and row-loading-reuse regressions

Now lock in the performance fix with tests that prove the code stops recomputing the same work.

Add two tests:

1. In `tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs`, add a test named something like `GetDiscoveryResult_returns_the_same_cached_result_for_the_same_ef_model`.
   - Call `GetDiscoveryResult(db)` twice on the same context/model.
   - Assert the returned result instance is the same cached object, or at minimum that the second call hits the cache rather than rebuilding a new result.
   - Keep the assertion simple and reference-based if possible.

2. In `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`, add a list-page query-count regression named something like `Get_duplicate_related_field_list_page_reads_customers_once`.
   - Reuse the existing `DuplicateRelationDbContext` / `TableSelectCountingInterceptor` pattern from the create-form test at `EfUiEndpointsTests.cs:780-840`.
   - Render the list page that exposes two FK-backed fields pointing at the same related table.
   - Assert the related table is selected only once for that request.
   - This should fail today because `BuildRelatedValueLookups(...)` re-reads related tables and the current list path does not share a request-scoped row cache.

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj --filter FullyQualifiedName~GetDiscoveryResult_returns_the_same_cached_result_for_the_same_ef_model
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter FullyQualifiedName~Get_duplicate_related_field_list_page_reads_customers_once
```

Expected: both FAIL before implementation.

### Step 6: Implement discovery caching and request-scoped row reuse

Make the performance fix without changing the public shape of the UI.

Target behavior:

- Cache `EntityDiscoveryResult` per EF `IModel` so repeated requests for the same model reuse the same discovery result.
- Replace the reflective `DbContext.Set<TEntity>()` lookup with a cached per-entity accessor, so `ReadRows(...)` does not rediscover the same method on every call.
- Introduce a small request-scoped row cache in `EfUiApplicationBuilderExtensions` and thread it through the list-view and form-option helpers so the same related table is not loaded twice in the same request.
- Keep the cache request-scoped; do **not** start caching table data across requests.
- Keep the JSON payload and HTML renderer contracts unchanged unless the cache refactor absolutely requires a new private helper.

A good implementation shape is a small internal loader/caching helper plus a `ConditionalWeakTable<IModel, EntityDiscoveryResult>` or equivalent for the discovery cache.

Rerun the two cache tests.

Expected: PASS.

### Step 7: Update docs and run the full release gates

Update `src/EfUi.AspNetCore/README.md` so it matches the hardened antiforgery behavior. Keep the wording concise:

- write forms and delete actions still include hidden tokens by default
- the token is now paired with a route-scoped cookie rather than being a bare protected nonce
- keep the existing limitations list accurate

If the route-name behavior should be documented for package consumers, add a brief note; otherwise keep the docs focused on the antiforgery change and current limitations.

Then run the repo’s normal verification gates in Release mode:

```bash
dotnet build EfUi.sln -c Release
dotnet test EfUi.sln -c Release --no-build
```

Expected: both exit 0.

## Test plan

- Add the new antiforgery replay regression in `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`.
- Add the schema-collision route tests in `tests/EfUi.Core.Tests/Metadata/EntityMetadataProviderTests.cs`.
- Add the discovery-cache and related-row query-count regressions in the same two test projects.
- Reuse the existing `DbCommandInterceptor` pattern already present in `EfUiEndpointsTests.cs` for query counting.
- Keep the tests focused on the user-visible contract: rejected replay, stable unique route names, and fewer redundant selects.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] The new antiforgery replay test exists and passes.
- [ ] The new schema-collision route tests exist and pass.
- [ ] The new discovery-cache test exists and passes.
- [ ] The new list-page query-count test exists and passes.
- [ ] `dotnet build EfUi.sln -c Release` exits 0.
- [ ] `dotnet test EfUi.sln -c Release --no-build` exits 0.
- [ ] `src/EfUi.AspNetCore/README.md` reflects the hardened antiforgery behavior.
- [ ] No files outside the in-scope list are modified (`git status`).
- [ ] `plans/README.md` status row is updated.

## STOP conditions

Stop and report back instead of improvising if:

- The current `EfUiRequestForgery` code no longer matches the excerpts above and the cookie-pairing fix needs a broader rewrite.
- Disambiguating route names would force a public breaking change for existing non-colliding URLs.
- The metadata cache would require stale cross-request table data or a global cache keyed by mutable strings.
- The row-loading reuse refactor starts leaking into `HtmlPageRenderer`, `EntityCrudService`, or other out-of-scope code paths.
- Any verification command fails twice after a reasonable fix attempt.

## Maintenance notes

- If future work adds more mounted EF UI prefixes, keep the antiforgery cookie name/path logic route-scoped so separate mounts do not collide.
- If more schema-aware route collisions appear later, extend the disambiguation helper in `EfEntityMetadataProvider` rather than introducing one-off special cases in callers.
- If the model changes at runtime in a future feature, revisit the `IModel`-scoped discovery cache; for the current repo, the model is stable enough that caching is appropriate.
- Keep query-count regressions focused on the exact table that is duplicated; do not make the tests depend on incidental SQL formatting.
