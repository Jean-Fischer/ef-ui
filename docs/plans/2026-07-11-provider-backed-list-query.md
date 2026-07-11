# Provider-Backed List Query Execution Implementation Plan

> **REQUIRED SUB-SKILL:** Use the executing-plans skill to implement this plan task-by-task.

**Goal:** Replace EF UI’s full-table in-memory list filtering, sorting, and paging with provider-backed result-window execution while preserving the existing routes, URL contract, HTML/JSON shapes, and form/CRUD behavior.

**Architecture:** Add a deep query module to `EfUi.Core` that receives resolved `EntityMetadata`, a scoped `DbContext`, and canonical `TableQuery` data. It validates visible-field query semantics, builds provider-translatable EF expressions, applies deterministic primary-key ordering, asynchronously fetches one result window, and enriches only the page’s related keys with display labels. `EfUi.AspNetCore` remains the HTTP adapter: it parses URL parameters, invokes the query module for HTML/JSON/post-delete list paths, and adapts projected values into the existing rendering models. There is deliberately no general in-memory fallback or speculative computed-label expression configuration.

**Tech Stack:** C#; .NET 8/9/10; EF Core relational APIs; SQLite test provider; ASP.NET Core minimal APIs; xUnit; FluentAssertions; `DbCommandInterceptor` for provider-execution assertions.

---

## Settled design constraints

- Keep the existing `filter.N.field`, `filter.N.op`, `filter.N.value`, `sort.N.field`, `sort.N.dir`, `offset`, and `limit` URL contract.
- Keep queryable fields limited to visible entity properties and existing one-hop FK display-label semantics.
- Bind raw equality values to CLR property types; support `contains` for text/display expressions.
- Let string matching follow provider/database collation rather than promising ordinal case-insensitivity.
- Query only mapped scalar display properties. Computed CLR display properties remain renderable but return structured visible query errors when queried.
- Invalid clauses use best-effort behavior: discard invalid clauses, execute valid clauses, and return all errors.
- Translation failures become structured query errors; never retry by materializing the full table.
- Use EF provider execution in production and SQLite-backed execution tests; do not retain a general in-memory query adapter.
- Apply primary-key ascending order when no user sort exists and append the primary key as a deterministic tie-breaker otherwise.
- Do not add a total-count query in this increment.
- Enrich only related keys present in the result window; do not load full related lookup tables.
- Do not wrap ordinary list reads in an explicit transaction.
- Propagate `HttpContext.RequestAborted` through asynchronous query execution.
- Migrate HTML list, JSON data, and post-delete list paths together.
- Leave form option loading and CRUD mutation behavior outside this increment.

## Functional impact to preserve or document

- Routes, bookmarks, query parameters, authorization, antiforgery, forms, and CRUD behavior remain unchanged.
- Unsorted row order becomes deterministic by primary key; this may visibly reorder existing unsorted lists.
- Mapped FK label filtering/sorting remains supported.
- Computed CLR labels such as `Employee.FullName` remain visible but cannot be provider-filtered or provider-sorted yet; the UI must show a query error instead of silently loading all rows.
- String matching follows the configured provider collation.
- No count or new pagination UI is introduced.

---

### Task 1: Add provider-backed query result types

**Files:**
- Create: `src/EfUi.Core/Query/EntityListQueryResult.cs`
- Create: `src/EfUi.Core/Query/EntityListQueryRow.cs`
- Create: `src/EfUi.Core/Query/EntityListQueryCell.cs`
- Create: `src/EfUi.Core/Query/EntityListQueryError.cs`
- Test: `tests/EfUi.Core.Tests/Query/EntityListQueryResultTests.cs`

**Step 1: Write the failing result-model tests**

Add tests proving the result model can represent:

- a stable row key;
- a cell’s formatted raw value and display text separately;
- optional related route metadata needed by the ASP.NET output adapter;
- applied filters and sorts;
- offset and limit;
- warnings and structured errors;
- no total-count field.

Use concrete records with immutable collections or read-only collection properties. Do not include HTML, hrefs, antiforgery markup, or row-action markup in these types.

**Step 2: Run the focused tests**

Run:

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj -c Release --filter FullyQualifiedName~EntityListQueryResultTests
```

Expected: FAIL because the query result types do not exist.

**Step 3: Implement the minimal result model**

Create records under `EfUi.Core.Query` with these responsibilities:

- `EntityListQueryResult`: projected rows, normalized applied query state, errors, warnings, offset, and limit.
- `EntityListQueryRow`: formatted primary-key value and projected cells by visible property name.
- `EntityListQueryCell`: formatted raw value, display text, and optional related route name.
- `EntityListQueryError`: field-scoped or query-scoped code/message data suitable for conversion to the existing visible error strings.

Keep these types independent of ASP.NET and HTML rendering.

**Step 4: Run the focused tests**

Run the same command. Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.Core/Query tests/EfUi.Core.Tests/Query/EntityListQueryResultTests.cs
git commit -m "feat: add list query result model"
```

---

### Task 2: Separate HTTP query parsing from execution policy

**Files:**
- Modify: `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs:~362-500`
- Create: `src/EfUi.AspNetCore/TableQueryRequestParser.cs`
- Create: `tests/EfUi.AspNetCore.Tests/TableQueryRequestParserTests.cs`
- Review: `src/EfUi.Core/Rendering/TableQuery.cs`

**Step 1: Write failing parser tests**

Cover:

- one or more `filter.N.*` and `sort.N.*` clauses;
- missing optional values;
- offset and limit defaults;
- invalid integer syntax and negative values;
- invalid sort direction;
- clause indexes discovered independently of key ordering;
- preserving field/operator/value text for the Core query module to validate;
- no dependency on `EntityMetadata` or EF Core in the parser.

The parser should report structural errors but must not decide whether a field is visible or whether an operator is supported by a specific entity.

**Step 2: Run parser tests**

```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj -c Release --filter FullyQualifiedName~TableQueryRequestParserTests
```

Expected: FAIL because parsing is embedded in the application-builder module.

**Step 3: Extract the HTTP adapter**

Move the query-string grammar into `TableQueryRequestParser`:

- accept `HttpRequest` or `IQueryCollection`;
- return `TableQuery` plus structural errors;
- preserve valid clause order by index;
- leave field capability and relationship semantics to the Core query module;
- keep the current fallback values (`offset = 0`, `limit = 50`).

Remove the old parser helpers and `BoundTableQuery` from `EfUiApplicationBuilderExtensions` after callers are migrated.

**Step 4: Run parser and existing query endpoint tests**

```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj -c Release --filter "FullyQualifiedName~TableQueryRequestParserTests|FullyQualifiedName~EfUiEndpointsTests"
```

Expected: PASS; existing endpoint output should remain unchanged before the executor is connected.

**Step 5: Commit**

```bash
git add src/EfUi.AspNetCore/TableQueryRequestParser.cs src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs tests/EfUi.AspNetCore.Tests/TableQueryRequestParserTests.cs
git commit -m "refactor: isolate HTTP table query parsing"
```

---

### Task 3: Build query capability and validation rules

**Files:**
- Create: `src/EfUi.Core/Query/EntityListQueryCapabilities.cs`
- Create: `src/EfUi.Core/Query/EntityListQueryValidator.cs`
- Create: `tests/EfUi.Core.Tests/Query/EntityListQueryValidatorTests.cs`
- Review: `src/EfUi.Core/Metadata/EntityMetadata.cs`
- Review: `src/EfUi.Core/Metadata/EntityPropertyMetadata.cs`

**Step 1: Write failing capability and validation tests**

Cover:

- visible scalar properties are queryable;
- unsupported or hidden fields are rejected while other valid clauses survive;
- `eq` is available for typed scalar values;
- `contains` is available for strings and mapped display strings;
- unsupported operators produce structured errors;
- a one-hop FK whose display property is a mapped scalar is queryable;
- a computed CLR display property with no mapped EF property is display-only and produces a structured error for filter/sort;
- duplicate filters and sorts preserve current best-effort ordering behavior;
- the primary key is not exposed as a special caller requirement—the executor adds deterministic ordering internally.

Use the existing metadata provider and SQLite model doubles where possible. Add the smallest test-only entity needed for an unmapped computed display property.

**Step 2: Run the focused tests**

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj -c Release --filter FullyQualifiedName~EntityListQueryValidatorTests
```

Expected: FAIL because capability and validation modules do not exist.

**Step 3: Implement capability discovery and validation**

Implement a Core query policy that:

- derives visible fields from `EntityMetadata.AllProperties`;
- inspects the EF `IModel` for mapped scalar properties;
- resolves one-hop FK metadata and mapped principal display properties;
- treats a display property name absent from the principal `IEntityType` as non-queryable, without disabling rendering;
- validates clauses independently so best-effort execution can continue;
- returns normalized valid filters/sorts and structured errors.

Do not add a host expression-registration option or capability marker to the public metadata model in this task.

**Step 4: Run focused and metadata tests**

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj -c Release --filter "FullyQualifiedName~EntityListQueryValidatorTests|FullyQualifiedName~EntityMetadataProviderTests"
```

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.Core/Query tests/EfUi.Core.Tests/Query/EntityListQueryValidatorTests.cs
git commit -m "feat: validate provider-backed list queries"
```

---

### Task 4: Implement typed provider filtering and deterministic ordering

**Files:**
- Create: `src/EfUi.Core/Query/EntityListQueryExecutor.cs`
- Create: `src/EfUi.Core/Query/ProviderQueryExpressionBuilder.cs`
- Create: `tests/EfUi.Core.Tests/Query/EntityListQueryExecutorTests.cs`
- Modify: `tests/EfUi.Core.Tests/TestDoubles/SampleModelDbContext.cs`

**Step 1: Write failing provider execution tests**

Seed enough SQLite rows to distinguish a database window from full-table materialization. Cover:

- typed equality for integer, nullable integer, Boolean, enum, GUID, and `DateTime` where test models support them;
- string `contains` using provider-translatable pattern matching;
- invalid clauses are discarded while valid clauses execute;
- unsupported value/operator combinations become structured errors;
- default primary-key ascending order;
- user sorts followed by primary-key tie-breaking;
- offset and limit are applied after filtering and ordering;
- empty result windows;
- request cancellation is honored before or during execution;
- unrelated database failures are not converted into query validation errors.

Keep assertions on returned results and structured errors, not exact SQL text.

**Step 2: Run executor tests to verify failure**

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj -c Release --filter FullyQualifiedName~EntityListQueryExecutorTests
```

Expected: FAIL because provider execution is not implemented.

**Step 3: Implement the provider-backed executor**

Implement asynchronous execution with a cancellation token:

1. Resolve the generic `DbSet<TEntity>` from `EntityMetadata.ClrType` using the same reflection/delegate approach only at the executor seam.
2. Validate the canonical query through the capability/validation module.
3. Build typed equality constants using the existing scalar-binding rules or a shared Core binding helper.
4. Build text pattern expressions using EF-translatable operations; do not force invariant lower-casing.
5. Apply filters before sorting and paging.
6. Apply each user sort in order, then append primary-key ascending as a tie-breaker; if no user sort exists, apply primary-key ascending directly.
7. Fetch only the requested entity window with `ToListAsync(cancellationToken)`.
8. Project raw property values and display text into the Task 1 result model.
9. Return valid applied clauses plus all validation/translation errors.

If an expected provider translation exception occurs, return a structured query error and no memory fallback. Do not catch unrelated database exceptions.

Do not add total-count execution or an explicit transaction.

**Step 4: Add provider-execution observation**

Add a test-only `DbCommandInterceptor` that records command text and parameters. Assert that a filtered, sorted, limited request produces SQL containing provider-side filtering and a window operation, without asserting provider-specific whitespace or exact SQL text. Add a regression test that would fail if the executor called `ToList()` before applying query operations.

**Step 5: Run executor and full Core tests**

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj -c Release --filter "FullyQualifiedName~EntityListQueryExecutorTests|FullyQualifiedName~EntityListQueryValidatorTests"
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj -c Release --no-restore
```

Expected: PASS.

**Step 6: Commit**

```bash
git add src/EfUi.Core/Query tests/EfUi.Core.Tests/Query tests/EfUi.Core.Tests/TestDoubles/SampleModelDbContext.cs
git commit -m "feat: execute list queries through EF providers"
```

---

### Task 5: Add bounded related-label enrichment

**Files:**
- Create: `src/EfUi.Core/Query/RelatedLabelEnricher.cs`
- Create: `tests/EfUi.Core.Tests/Query/RelatedLabelEnricherTests.cs`
- Modify: `src/EfUi.Core/Query/EntityListQueryExecutor.cs`
- Review: `src/EfUi.Core/Rendering/EntityDisplayLabelResolver.cs`

**Step 1: Write failing related-label tests**

Cover:

- mapped scalar FK display labels are applied to returned page rows;
- only related keys present in the page are requested;
- missing related rows fall back to the formatted raw FK value;
- null FK values render empty;
- one-hop FK label filtering and sorting happen before the entity window is selected;
- computed CLR display labels render when projected but produce a query error when used in filter/sort;
- no explicit transaction is opened for list reads;
- multiple FK properties to the same related entity keep their own display-property overrides.

Use command observation to assert that a large related table is not fully materialized.

**Step 2: Run focused tests**

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj -c Release --filter FullyQualifiedName~RelatedLabelEnricherTests
```

Expected: FAIL because bounded enrichment does not exist.

**Step 3: Implement bounded enrichment and related expressions**

Implement two distinct paths:

- **Query path:** build a correlated provider expression for supported one-hop mapped display properties so filters and sorts execute before `Skip`/`Take`, including scalar FK-only relationships where the dependent has no CLR navigation.
- **Display path:** after the entity window is fetched, collect distinct non-null FK keys from that page, query only those related rows, resolve labels through `EntityDisplayLabelResolver`, and merge labels into projected cells.

Preserve the current raw-value fallback when a related row is missing. Keep display-only computed labels available for rendering.

**Step 4: Run Core query tests**

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj -c Release --filter "FullyQualifiedName~Query"
```

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.Core/Query tests/EfUi.Core.Tests/Query
git commit -m "feat: enrich list windows with bounded related labels"
```

---

### Task 6: Adapt ASP.NET list routes to the deep query module

**Files:**
- Modify: `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs:78-285, 317-900`
- Create: `src/EfUi.AspNetCore/RenderedListViewAdapter.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/EscapedStringKeyRoutingTests.cs`

**Step 1: Add failing route integration assertions**

Add endpoint tests proving:

- HTML and JSON list requests return the same filtered/sorted rows and normalized query state;
- post-delete HTML uses the same default query preparation as a normal HTML list;
- mapped FK labels and edit links remain unchanged;
- missing related rows retain raw-value fallback;
- computed label queries return visible errors without falling back to full-table loading;
- request cancellation is passed to the query executor;
- forms, create/update/delete behavior, antiforgery, and authorization remain unchanged.

**Step 2: Run focused endpoint tests**

```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj -c Release --filter "FullyQualifiedName~EfUiEndpointsTests|FullyQualifiedName~ChinookEndpointsTests|FullyQualifiedName~EscapedStringKeyRoutingTests"
```

Expected: FAIL until the route module uses provider-backed execution.

**Step 3: Add the ASP.NET result adapter**

Create `RenderedListViewAdapter` to convert the projected Core result into the existing `RenderedListView`:

- copy normalized filters, sorts, errors, warnings, offset, and limit;
- convert projected cells to `RenderedListCell`;
- build related edit hrefs from route prefix, related route name, and escaped raw key;
- leave row-action markup to the existing rendering adapters;
- keep the adapter independent of EF query construction.

**Step 4: Replace list preparation in all three paths**

Update `RenderEntityList`, `RenderEntityListData`, and `DeleteEntityAsync` to:

- resolve `DbContext` and discovery once;
- resolve `EntityMetadata` once;
- parse HTTP query parameters through `TableQueryRequestParser`;
- invoke the Core executor asynchronously with `request.HttpContext.RequestAborted`;
- adapt the result to `RenderedListView`;
- keep HTML and JSON serialization unchanged.

Remove list-only use of `RequestRowCache`, `ReadRowsAccessors`, `ReadRowsCore`, `BuildRelatedValueLookups`, `ApplyTableQuery`, and their reflection-based row materialization helpers. Preserve a separate full-row path for form option loading so that form behavior remains outside this increment.

Make the HTML list handler asynchronous and await query execution rather than blocking with `GetAwaiter().GetResult()`.

**Step 5: Run focused endpoint tests**

```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj -c Release --filter "FullyQualifiedName~EfUiEndpointsTests|FullyQualifiedName~ChinookEndpointsTests|FullyQualifiedName~EscapedStringKeyRoutingTests"
```

Expected: PASS.

**Step 6: Commit**

```bash
git add src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs src/EfUi.AspNetCore/RenderedListViewAdapter.cs tests/EfUi.AspNetCore.Tests
git commit -m "refactor: route lists through provider-backed query execution"
```

---

### Task 7: Preserve and tighten behavioral coverage

**Files:**
- Modify: `tests/EfUi.Core.Tests/Rendering/HtmlPageRendererTests.cs`
- Modify: `tests/EfUi.Core.Tests/Rendering/EntityDisplayLabelResolverTests.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiProductionTests.cs`
- Review: `tests/EfUi.AspNetCore.Tests/Browser/ChinookPlaywrightTests.cs`

**Step 1: Identify tests that assert implementation rather than behavior**

Remove or rewrite tests that depend on:

- `RequestRowCache` internals;
- LINQ-to-Objects ordering assumptions;
- exact provider SQL text;
- full related lookup materialization;
- concrete construction of query internals.

Keep tests that assert the existing package-facing behavior through HTML, JSON, Core query results, and browser flows.

**Step 2: Add compatibility assertions**

Cover:

- route and URL contract preservation;
- deterministic default and tie-break ordering;
- mapped FK filter/sort behavior;
- provider-collation documentation behavior using SQLite;
- visible unsupported errors for computed CLR labels;
- no count field added to payloads;
- HTML/JSON/post-delete consistency;
- authorization and production enablement behavior.

**Step 3: Run the complete test projects**

```bash
dotnet test tests/EfUi.Core.Tests/EfUi.Core.Tests.csproj -c Release --no-build
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj -c Release --no-build
```

Expected: PASS.

**Step 4: Run browser coverage if Playwright is available**

```bash
mise run test-browser
```

Expected: PASS. If browsers are not installed, run `mise run playwright-install` first and record that setup separately.

**Step 5: Commit**

```bash
git add tests
git commit -m "test: cover provider-backed list semantics"
```

---

### Task 8: Update package-facing documentation

**Files:**
- Modify: `README.md`
- Modify: `src/EfUi.AspNetCore/README.md`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`

**Step 1: Write documentation assertions**

Extend the existing README test expectations to require documentation for:

- provider-backed filtering, sorting, and paging;
- deterministic primary-key ordering;
- provider/database collation for text matching;
- mapped scalar display-label query support;
- computed CLR display labels being display-only for query purposes;
- no total-count guarantee in the current list contract.

**Step 2: Update the package documentation**

Remove the obsolete statement that very large tables are rendered through in-memory row loading. Replace it with concise package-facing behavior:

- list filtering, sorting, and paging execute through the registered EF provider;
- only the requested result window and its related display keys are materialized;
- text matching follows provider collation;
- computed CLR display properties still render but are not provider-queryable without a mapped scalar or future explicit expression support.

Do not document internal class names or the test interceptor.

**Step 3: Run documentation and endpoint tests**

```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj -c Release --filter "FullyQualifiedName~Readme|FullyQualifiedName~EfUiEndpointsTests"
```

Expected: PASS.

**Step 4: Commit**

```bash
git add README.md src/EfUi.AspNetCore/README.md tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs
git commit -m "docs: describe provider-backed list queries"
```

---

### Task 9: Final multi-target verification

**Files:**
- Review: `EfUi.sln`
- Review: `Directory.Build.props`
- Review: `src/EfUi.AspNetCore/README.md`

**Step 1: Build all target frameworks**

```bash
dotnet build EfUi.sln -c Release
```

Expected: PASS for .NET 8, .NET 9, and .NET 10 targets.

**Step 2: Run all tests**

```bash
dotnet test EfUi.sln -c Release --no-build
```

Expected: PASS for all Core and ASP.NET Core test targets.

**Step 3: Run the sample host smoke path**

```bash
dotnet run --project src/EfUi.SampleHost --framework net8.0
```

Verify manually:

- `/simple/users` renders the same table shape and related links;
- filter/sort/offset/limit requests return provider-backed windows;
- unsorted results use primary-key order;
- invalid clauses show visible errors while valid clauses still apply;
- computed display labels still render and produce a visible query error when queried;
- create/edit/delete and antiforgery behavior remain intact;
- `/chinook/tracks` and related-row navigation continue to work.

Stop the host after the smoke pass. Do not commit sample databases or build outputs.

**Step 4: Check repository cleanliness**

```bash
git status --short
```

Expected: only intended source, test, and documentation changes are present; no `bin/`, `obj/`, `.artifacts/`, sample databases, or scan output is staged.

**Step 5: Commit any required final correction**

Only if verification found a real issue:

```bash
git add <verified-files>
git commit -m "fix: complete provider-backed list query migration"
```

---

## Completion criteria

The implementation is complete when:

- list filtering, sorting, and paging execute through EF provider queries;
- no list path calls `ToList()` before query operations are applied;
- related lookup loading is limited to keys in the result window;
- deterministic primary-key ordering is covered by tests;
- mapped FK display-label filtering/sorting is covered by tests;
- computed CLR display-label query attempts return visible structured errors without memory fallback;
- HTML, JSON, and post-delete list paths share the same query preparation;
- cancellation propagates from HTTP requests to EF execution;
- forms, CRUD, authorization, antiforgery, routes, and URL parameters remain compatible;
- documentation describes the new provider-backed behavior and its computed-label limitation;
- all target frameworks build and all tests pass.
