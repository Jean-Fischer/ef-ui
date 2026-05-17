# Chinook Multi-Context Sample Host Design

**Date:** 2026-05-17

## Objective

Extend the sample host so it exposes multiple EF UI mounts backed by different EF Core `DbContext` types:

- `/simple` for the existing `SampleDbContext`
- `/chinook` for a scaffolded `ChinookDbContext` generated from `db/chinook.db`

The goal is to make both mounts available in both development and production, keep the root page minimal, and allow real CRUD testing against the Chinook database.

## Validated Decisions

- Keep the implementation **simple and explicit** in the sample host.
- Use **two direct `UseEfUi(...)` calls** rather than building a config-driven or plugin-style registration system.
- Rename the current sample route from **`/efui` to `/simple`** so it does not look like a special default route.
- Add a second mount at **`/chinook`** backed by a scaffolded EF Core model.
- Make **development and production behave the same** for these mounts.
- Keep the root page as a **tiny HTML landing page** with links to both mounts.
- Chinook should be **editable**, not read-only, so create/update/delete behavior can be tested through EF UI.
- Avoid broad library redesign unless the Chinook integration reveals a real limitation.

## Evaluated Approaches

### Option A — Two explicit mounts in the sample host (**recommended**)

Register both contexts in DI and call `UseEfUi(...)` twice with different route prefixes.

**Pros**
- fastest route to a working demo
- smallest change surface
- easy to understand when reading `Program.cs`
- proves the library can support multiple mounts already

**Cons**
- not configuration-driven
- not yet a reusable pattern for arbitrary future contexts

### Option B — Config-driven multi-context registration

Describe mounts in configuration and loop over them during startup.

**Pros**
- more flexible for future growth
- less host code duplication

**Cons**
- more design and validation work now
- adds indirection before the real Chinook demo is proven
- unnecessary for the current goal

### Option C — Separate Chinook host project

Create a dedicated host for Chinook and leave the current sample host untouched.

**Pros**
- isolates concerns
- avoids changing the sample host flow

**Cons**
- more files and host duplication
- weaker demonstration of multiple mounts in one app
- adds maintenance cost immediately

## Recommended Repository Shape

```text
src/EfUi.SampleHost/
  Program.cs
  appsettings.json
  Data/
    SampleDbContext.cs
    SampleDbSeeder.cs
  Chinook/
    ChinookDbContext.cs
    ...scaffolded entity files...

tests/EfUi.AspNetCore.Tests/
  EfUiEndpointsTests.cs
  EfUiProductionTests.cs
  ...possible new Chinook-focused endpoint tests...

docs/plans/
  2026-05-17-chinook-multi-context-design.md
```

## Startup Shape

The sample host should register two independent EF Core contexts:

1. `SampleDbContext` using the existing SQLite sample database
2. `ChinookDbContext` using `db/chinook.db`

The app should continue seeding only the sample database. Chinook should not be seeded or recreated on startup; it should be used as-is.

After service registration, the host should map:

```csharp
app.UseEfUi(options =>
{
    options.DbContextType = typeof(SampleDbContext);
    options.RoutePrefix = "/simple";
    options.EnableInProduction = true;
});

app.UseEfUi(options =>
{
    options.DbContextType = typeof(ChinookDbContext);
    options.RoutePrefix = "/chinook";
    options.EnableInProduction = true;
});
```

This keeps the sample host explicit and avoids introducing a higher-level abstraction before it is needed.

## Root Page

The existing root redirect should be replaced with a very small HTML landing page.

It should do only one thing: expose links to the available UI mounts.

Expected behavior:

- `/` returns a minimal HTML page
- page includes links to:
  - `/simple`
  - `/chinook`

The page should remain intentionally plain:

- a heading
- a short sentence
- a list of links

No layout system, controller, Razor page, or dashboard is needed.

## Chinook Scaffolding Strategy

The Chinook model should be scaffolded from the SQLite database file at:

```text
db/chinook.db
```

Recommended command shape:

```bash
dotnet ef dbcontext scaffold "Data Source=db/chinook.db" Microsoft.EntityFrameworkCore.Sqlite --project src/EfUi.SampleHost --startup-project src/EfUi.SampleHost --output-dir Chinook --context-dir Chinook --context ChinookDbContext --namespace EfUi.SampleHost.Chinook --force
```

The scaffolded files should stay grouped under a dedicated folder/namespace so they are clearly machine-generated and separate from the hand-written sample model.

## CRUD Expectations

Chinook should be exposed as a normal editable EF UI surface.

That means the first implementation should aim to allow:

- listing entities
- viewing create forms
- creating rows where supported
- editing rows
- deleting rows

The design assumes some scaffolded entities may reveal limitations in current EF UI support, especially around:

- composite keys
- generated values
- relationship-heavy entities
- columns that are technically writable but awkward for generic CRUD

The initial implementation should not pre-emptively add filtering or exclusions. Instead, it should scaffold the model, run the app, and then respond to any concrete incompatibilities discovered during verification.

## Production Behavior

There should be no difference between development and production for the sample host mounts.

Expected behavior in both environments:

- `/simple` is available
- `/chinook` is available
- `/` shows the same tiny landing page

This is a host-level decision for the demo application, not a requirement to change the default library posture for unrelated consumers.

## Testing Strategy

### Focused behavior checks

Add or update tests to prove:

- `GET /` returns a page linking to `/simple` and `/chinook`
- `GET /simple` returns the existing sample index
- `GET /chinook` returns a Chinook-backed index
- production host tests still succeed for the mounted routes

### Chinook CRUD smoke coverage

Add at least one focused test for a simple Chinook entity that exercises edit behavior through EF UI.

Selection criteria for the first tested entity:

- scalar primary key
- mostly scalar writable columns
- minimal relationship complexity

This keeps the first proof of CRUD behavior stable.

### Full verification

Run:

```bash
dotnet test EfUi.sln
```

Then manually verify both URLs in production mode.

## Risks and Response Strategy

### Risk: scaffolded entities expose unsupported shapes

**Response:** treat this as product feedback, not a reason to over-design early. Fix the specific limitation or temporarily narrow the test target if necessary.

### Risk: Chinook updates mutate the checked-in SQLite file

**Response:** accept that for the first local demo, but be deliberate during testing. If this becomes annoying, a later step can copy the database to a temp/demo location before startup.

### Risk: too much host-specific code leaks into the core library

**Response:** keep the multi-mount orchestration in the sample host unless repeated patterns clearly justify promotion into reusable library APIs.

## Success Criteria

The design is successful when all of the following are true:

- running the sample host exposes a root page with two links
- `/simple` serves the existing sample UI
- `/chinook` serves a scaffolded Chinook UI
- both mounts are available in production
- at least one Chinook entity can be updated through EF UI
- the implementation remains small and explicit rather than prematurely abstract
