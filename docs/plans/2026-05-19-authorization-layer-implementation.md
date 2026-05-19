# Authorization Layer Implementation Plan

> **REQUIRED SUB-SKILL:** Use the executing-plans skill to implement this plan task-by-task.

**Goal:** Add an opt-in, dotnet-native authorization layer to EF UI using standard ASP.NET Core roles, plus a sample-host auth switch for validating anonymous, ReadOnly, and Edit flows.

**Architecture:** Keep EF UI auth-agnostic and attach standard endpoint authorization metadata when `RequireAuthorization` is enabled. Browsing routes should allow `ReadOnly` and `Edit`, while mutation routes should require `Edit`. The sample host will provide a tiny dev-only cookie auth harness so the behavior is easy to test in a browser without changing the library’s default behavior.

**Tech Stack:** .NET 8, ASP.NET Core minimal APIs, ASP.NET Core authorization, xUnit, FluentAssertions, WebApplicationFactory.

---

### Task 1: Add opt-in authorization options to EF UI

**Files:**
- Modify: `src/EfUi.AspNetCore/EfUiOptions.cs`
- Modify: `src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs`
- Test: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Test: `tests/EfUi.AspNetCore.Tests/EfUiProductionTests.cs`

**Step 1: Write the failing test**

Add tests that prove:
- EF UI routes remain public when `RequireAuthorization` is `false`
- when `RequireAuthorization` is `true`, browsing routes require `ReadOnly` or `Edit`
- when `RequireAuthorization` is `true`, POST routes require `Edit`
- unauthenticated requests get 401
- authenticated-but-wrong-role requests get 403

Start with one simple endpoint metadata test or a request test against `/simple` and `/chinook` using a test auth scheme.

**Step 2: Run test to verify it fails**

Run:

```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter FullyQualifiedName~Authorization --no-restore
```

Expected: FAIL because the options and/or route authorization are not implemented yet.

**Step 3: Write minimal implementation**

Add `RequireAuthorization`, `ReadOnlyRoleName`, and `EditRoleName` to `EfUiOptions`. Update route mapping so the EF UI endpoints attach standard authorization metadata only when the option is enabled.

Use the simplest possible standard ASP.NET Core shape. Prefer endpoint metadata or route groups over any custom middleware.

**Step 4: Run test to verify it passes**

Run the same test command again.

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.AspNetCore/EfUiOptions.cs src/EfUi.AspNetCore/EfUiApplicationBuilderExtensions.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs tests/EfUi.AspNetCore.Tests/EfUiProductionTests.cs
git commit -m "feat: add opt-in ef ui authorization"
```

### Task 2: Add a dev-only auth switch to the sample host

**Files:**
- Modify: `src/EfUi.SampleHost/Program.cs`
- Possibly create: `src/EfUi.SampleHost/Auth/*`
- Modify: `src/EfUi.SampleHost/EfUi.SampleHost.csproj` if an extra package is needed, though prefer staying inside `Microsoft.AspNetCore.App`
- Test: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`
- Test: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`

**Step 1: Write the failing test**

Add integration tests that prove the sample host can switch between anonymous, ReadOnly, and Edit modes. If browser automation is too heavy, a standard cookie-auth test handler is acceptable for the automated tests, with a small manual verification note for the button UI.

Also add a test that the Chinook mount is protected when auth is enabled.

**Step 2: Run test to verify it fails**

Run:

```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter FullyQualifiedName~Chinook --no-restore
```

Expected: FAIL until the sample host auth harness exists.

**Step 3: Write minimal implementation**

Add a tiny cookie auth setup to the sample host and render three dev-only buttons on the root page:
- Anonymous
- ReadOnly
- Edit

Wire the buttons to simple sign-in/sign-out endpoints that issue the role claims. Protect the Chinook mount by setting `RequireAuthorization = true` there, while leaving the simple mount available for comparison.

**Step 4: Run test to verify it passes**

Run the same targeted Chinook/sample-host tests.

Expected: PASS.

**Step 5: Commit**

```bash
git add src/EfUi.SampleHost/Program.cs src/EfUi.SampleHost/EfUi.SampleHost.csproj tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs
git commit -m "feat: add sample host auth switch"
```

### Task 3: Add authorization integration tests with test auth

**Files:**
- Modify or create: `tests/EfUi.AspNetCore.Tests/TestAuth/*`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiApplicationFactory.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/EfUiEndpointsTests.cs`
- Modify: `tests/EfUi.AspNetCore.Tests/ChinookEndpointsTests.cs`

**Step 1: Write the failing test**

Create a reusable test authentication handler and factory configuration so the suite can simulate:
- anonymous
- ReadOnly
- Edit

Write tests that cover both read and mutation endpoints.

**Step 2: Run test to verify it fails**

Run:

```bash
dotnet test tests/EfUi.AspNetCore.Tests/EfUi.AspNetCore.Tests.csproj --filter FullyQualifiedName~Authorization --no-restore
```

Expected: FAIL until the test auth infrastructure is in place.

**Step 3: Write minimal implementation**

Implement the test auth handler, helper claims generation, and factory overrides. Keep it local to the test project and avoid coupling it to production code.

**Step 4: Run test to verify it passes**

Run the same authorization-focused test command.

Expected: PASS.

**Step 5: Commit**

```bash
git add tests/EfUi.AspNetCore.Tests
git commit -m "test: cover ef ui authorization roles"
```

### Task 4: Run the full suite and verify no regressions

**Files:**
- All changed files

**Step 1: Run the full tests**

Run:

```bash
dotnet test EfUi.sln --no-restore
```

Expected: PASS.

**Step 2: Spot-check the sample host manually**

Start the sample host and verify:
- anonymous users are blocked from protected Chinook pages
- ReadOnly can browse but cannot mutate
- Edit can do everything
- `/simple` still behaves normally

**Step 3: Commit any final cleanup**

If the spot-check reveals a cleanup issue, fix it and rerun the full suite before committing.

**Step 4: Commit**

```bash
git add .
git commit -m "feat: finish ef ui authorization support"
```

## Notes

- Keep the default behavior unchanged when authorization is disabled.
- Prefer standard ASP.NET Core primitives over custom abstractions.
- Let the framework return 401/403; do not add a custom access-denied page.
- Keep the sample host auth harness dev-only and library-free.
