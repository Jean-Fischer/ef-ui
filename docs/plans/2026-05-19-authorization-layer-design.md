# Authorization Layer Design

## Goal

Add a very small, dotnet-native authorization layer to EF UI that stays fully dependent on the host application's authentication setup. The feature should be opt-in, use standard ASP.NET Core authorization concepts, and support the common two-role model of `ReadOnly` and `Edit`.

The design should keep the library simple:

- no custom identity store
- no custom authorization framework
- no EF UI-specific permissions model beyond role checks
- no change to the default behavior unless the host enables it

## Validated decisions

- EF UI should rely entirely on the host application's authentication method.
- Authorization must be opt-in.
- The default role split should be `ReadOnly` for browsing and `Edit` for mutations.
- Browsing routes should be available to both `ReadOnly` and `Edit` users.
- Create, update, and delete routes should require `Edit`.
- 401/403 responses are acceptable for unauthenticated or underprivileged requests.
- The sample host should include a lightweight dev-only way to switch between anonymous, `ReadOnly`, and `Edit` profiles.
- The sample host should protect the Chinook mount so the auth flow is easy to test.

## Recommended approach

Use standard ASP.NET Core endpoint authorization metadata directly on the mapped EF UI routes.

### Suggested options

Extend `EfUiOptions` with a small set of auth settings:

```csharp
public sealed class EfUiOptions
{
    public Type DbContextType { get; set; } = null!;
    public string RoutePrefix { get; set; } = "/efui";
    public bool EnableInProduction { get; set; }

    public bool RequireAuthorization { get; set; }
    public string ReadOnlyRoleName { get; set; } = "ReadOnly";
    public string EditRoleName { get; set; } = "Edit";
}
```

`RequireAuthorization` keeps the feature opt-in. The role names stay configurable, but the defaults are the simple roles we want.

### Endpoint mapping rules

When `RequireAuthorization` is `false`, EF UI behaves exactly as it does today.

When `RequireAuthorization` is `true`:

- all EF UI routes get authorization metadata
- browsing routes require either `ReadOnly` or `Edit`
- mutation routes require `Edit`

This should be attached with standard `AuthorizeAttribute` metadata, not a custom middleware layer. In minimal API terms, the simplest shape is to map routes as usual and add `RequireAuthorization(...)` metadata with role names.

### Route classification

#### Browsing routes

These should be accessible to both roles:

- landing page
- entity list page
- entity list data endpoint
- create form
- edit form
- static EF UI assets

The create and edit forms are read-only views of the form UI until the user submits a POST.

#### Mutation routes

These should require `Edit` only:

- create POST
- update POST
- delete POST

## Runtime flow

### Without authorization

The current pipeline remains unchanged:

1. host builds the app
2. EF UI maps routes
3. users can browse and mutate if the host otherwise permits access

### With authorization enabled

1. host builds the app and configures its normal authentication/authorization middleware
2. EF UI maps the same routes, but with role metadata attached
3. the ASP.NET Core authorization system handles anonymous and role checks
4. unauthenticated requests receive framework-standard 401 responses
5. authenticated but underprivileged requests receive framework-standard 403 responses

EF UI does not need to know how the user authenticated. Cookies, bearer tokens, Entra, or any other ASP.NET Core-compatible scheme should work as long as the host supplies role claims.

## Sample host testing harness

To make the feature easy to try locally, the sample host should include a tiny development auth switch.

### Proposed sample host behavior

- keep one mount open for comparison, such as `/simple`
- protect the Chinook mount with `RequireAuthorization = true`
- add a small dev-only auth UI on the landing page
- provide buttons for:
  - Anonymous
  - ReadOnly
  - Edit
- use a standard cookie auth setup in the sample host so the selected profile persists across requests

This is intentionally sample-host-only. EF UI should still remain auth-agnostic.

### Why this is useful

This gives us a realistic way to test the authorization behavior in a browser without inventing a special EF UI test mode. It also lets us verify:

- anonymous access is blocked
- `ReadOnly` can browse but not mutate
- `Edit` can do everything
- the host remains in control of authentication

## Error handling and safety

The authorization layer should fail closed when enabled.

- If `RequireAuthorization` is off, no auth metadata should be added.
- If the host does not configure authentication middleware, the normal ASP.NET Core behavior should apply.
- If the user is authenticated but lacks the right role, let the framework return 403.
- Do not render a custom access-denied page unless a later requirement appears.
- Do not introduce a second permission model alongside roles.

The design should be conservative and easy to reason about.

## Testing strategy

The feature needs both automated and manual verification.

### Library integration tests

Add tests that use a fake or test authentication scheme so the auth flow can be exercised without depending on a real identity provider.

Cover at least these scenarios:

1. **Authorization disabled**
   - EF UI routes remain accessible exactly as before.

2. **Anonymous user with authorization enabled**
   - browsing routes return 401
   - mutation routes return 401

3. **Authenticated `ReadOnly` user**
   - browsing routes succeed
   - mutation routes return 403

4. **Authenticated `Edit` user**
   - browsing routes succeed
   - mutation routes succeed

5. **Route metadata sanity**
   - browse endpoints carry the expected role metadata
   - mutation endpoints carry `Edit` metadata only

### Sample host manual verification

Validate in the browser using the auth switch buttons:

- anonymous should fail on the protected Chinook mount
- ReadOnly should browse Chinook and fail on mutations
- Edit should fully operate on Chinook
- `/simple` should remain useful as the non-protected comparison mount

## Options considered

### Option A — no explicit EF UI option

Rely only on whatever auth the host happens to configure. This is minimal, but it makes behavior harder to reason about and harder to test in a predictable way.

### Option B — custom authorization subsystem

This would let EF UI define its own roles and permissions, but it adds a layer that duplicates what ASP.NET Core already provides.

### Option C — opt-in endpoint authorization with roles (**recommended**)

This keeps the implementation tiny, uses standard ASP.NET Core primitives, and remains easy to test.

## Non-goals

- custom user management
- EF UI-specific login pages
- policy-based permission graphs
- claims transformation inside EF UI
- fine-grained per-entity permissions
- a custom access denied experience

## Implementation note

If we later find that route groups make the mapping cleaner, that is fine, but the important part is that the final behavior should still be standard ASP.NET Core authorization metadata on the EF UI endpoints.
