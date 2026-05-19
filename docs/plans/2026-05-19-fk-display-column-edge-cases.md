# FK Display Column Edge Cases and Decision Matrix

## Goal

List the edge cases around FK display-column customization so we can decide, together, which ones are worth supporting now.

This is intentionally a discussion document, not an implementation plan. The point is to separate:

- **relevant** cases we are likely to hit,
- **used** cases that matter in real apps,
- **solvable** cases with a small code change,
- **too complicated** cases that should wait.

## Current behavior today

Before adding any more support, the current system behaves like this:

- entities must have a single primary key to be listed by EF UI
- shared join entities without a single PK are skipped
- regular entities with composite PKs currently throw during metadata discovery
- FK display labels fall back to a heuristic: `Name`, `Title`, `Email`, then PK
- a related entity row that cannot be found falls back to the raw FK value and does not link
- `EfUiDisplayColumn` can be applied to a navigation property or to the principal entity type
- the chosen display property is used for list cells, FK links, related-row pickers, and filtering/sorting display text

That means the new feature already has a reasonable default path. The question is which special cases should stay simple, and which ones deserve explicit support.

## Decision criteria

For each edge case, I suggest we judge it by four questions:

1. **Relevant?** Will a real user hit this in a normal EF model?
2. **Used?** Does it show up often enough to justify support?
3. **Solvable?** Can we support it without a large redesign?
4. **Too complicated?** Would supporting it drag in lots of special rules?

## Edge cases to decide on

| Edge case | Why it matters | Current behavior | Likely complexity | Suggested stance |
|---|---|---:|---:|---|
| Entity has no primary key | EF Core supports keyless types, and users may expect them to appear somewhere | Currently not supported by list metadata; single-PK is required | Medium/High | Probably **not now** unless we decide keyless read-only views are valuable |
| Entity has a composite primary key | Some domains use natural composite keys | Current metadata discovery throws | Medium | Probably **not now**; list URLs and edit routes get awkward |
| FK is composite | Some relationships use multiple columns | Current FK display logic assumes single-column FK | High | Probably **not now** |
| Principal entity has multiple FKs from different dependents | Same entity shown in different contexts may need different labels | Already supported by navigation-property override design | Low | **Yes** |
| Same principal entity has both class-level default and FK-level override | We need predictable precedence | FK-level override wins today | Low | **Yes** |
| Attribute points to a property that does not exist | Easy typo case | Falls back safely if we code it defensively | Low | **Yes**, fail open |
| Attribute placed on a non-navigation property | Misuse or confusion | Not explicitly handled yet | Low | **Yes**, ignore safely |
| Attribute placed on a class that is not a mapped entity | Misuse or dead code | Not explicitly handled yet | Low | **Yes**, ignore safely |
| Display property exists but value is null/empty/whitespace | Common with incomplete data | Current heuristic falls through | Low | **Yes**, fallback to next candidate |
| Display property exists but is not a string | e.g. `Code` is numeric or custom object | Uses `ToString()` today | Low | **Yes**, keep `ToString()` fallback |
| Display property is not unique | Two related rows may show the same label | Allowed today | Medium | **Maybe**; acceptable if links still disambiguate the actual row |
| Related row is missing in the database | Broken FK or filtered data set | Falls back to raw FK value, no link | Low | **Yes**, keep this |
| Entity has self-referencing FK | e.g. parent/manager relationships | Should work if navigation exists | Medium | **Probably yes** if it falls out naturally |
| Multiple navs to the same principal entity | Billing customer vs shipping customer | The main reason for per-navigation override | Low | **Yes** |
| No navigation property exists for the FK | Shadow FK or scalar-only model | Hard to place a per-FK attribute | Medium | **Probably not** unless we add a fluent/API or scalar attribute later |
| Shadow FK property | EF has a FK but no CLR property | Hard to expose in the current attribute model | Medium | **Probably not now** |
| Many-to-many skip navigation | Related labels shown in pickers, not FK cells | Already works through related-row label resolution | Medium | **Maybe** only if label override should affect pickers consistently |
| One-to-many collection field | Related rows are shown as selectable children | Uses same label resolver today | Medium | **Yes**, if the chosen display property is reused there |
| Shared join entity with payload | Hybrid management-link scenario | Currently hidden from list metadata | High | **Probably not** for display-column customization |
| Owned types / value objects | Not really FK targets, but can appear in EF model | Not a normal FK display problem | High | **No** |
| Inheritance hierarchies (TPH/TPT/TPC) | Different derived types may expose different display columns | Could be awkward to resolve consistently | Medium/High | **Maybe later** |
| Principal entity route exists but list page is not user-facing | Technical tables / support entities | Current route discovery may include some entities | Low | **Probably not**; hide or skip those entities instead |
| Display property should differ for list vs picker vs filter | Could be confusing if each screen shows a different label | Current plan reuses one resolved value everywhere | Medium | **No**, keep one label per relationship |
| Filtering on display label vs raw FK value | Users expect both “label search” and direct raw-key URLs | Current query logic supports label plus raw-eq fallback | Medium | **Yes**, keep both behaviors |
| Sorting by display label vs raw key | Should match what the user sees | Current plan sorts by display label when related lookup exists | Low | **Yes** |
| Related entity table has lots of rows | Display resolution can become expensive | Current implementation reads related rows into lookups | Medium | **Maybe**; probably acceptable for small/medium tables, not for huge lookup sets |
| Display property depends on additional navigation loading | Example: display uses a computed property or nested relation | Current resolver only reads simple CLR properties | High | **No** |
| Display property is computed / unmapped | No real CLR property or database column | Not supported by simple property-name approach | Medium | **Probably not now** |
| Attribute conflicts with user expectation on a shared principal entity | One default label is not enough for all contexts | Per-FK override handles most cases | Low | **Yes** via navigation override |

## Practical shortlist

If we want to keep the feature small, I think the worthwhile shortlist is:

### Worth supporting now

- per-navigation override
- class-level default
- fallback to current heuristic
- missing/invalid attribute values ignored safely
- null/empty display values fall back safely
- raw FK fallback when related row is missing
- same display choice used in list cells, links, pickers, and filters
- raw-key equality still works for direct URLs

### Worth considering, but maybe later

- self-referencing relationships
- many-to-many label reuse audits
- inheritance hierarchies
- uniqueness warnings for display labels
- performance optimizations for large lookup tables

### Probably not worth supporting now

- entities without primary keys
- composite primary keys
- composite foreign keys
- shadow FK only relationships with no navigation property
- computed / nested / unmapped display values
- owned-type display customization

## Special note: entity without a primary key

This is the example you gave, and it is probably the most important hard boundary.

Right now EF UI already assumes the entity has a single primary key. That assumption is baked into:

- route generation for list pages
- edit URL generation
- row identity in rendered tables
- lookup and link generation for related rows

So for a keyless entity, we would need to decide whether it is:

1. **excluded entirely** from EF UI,
2. **shown read-only only**, or
3. **supported with a synthetic identity**.

Option 3 is the complicated one. It would require a separate identity model and probably a different rendering contract.

My current bias:

- **skip keyless entities for now**
- if we ever need them, treat them as a separate read-only feature rather than part of FK display customization

## Graceful error reporting

If an entity or relationship cannot be loaded, we should prefer a clear, consolidated error report over a silent skip.

A good pattern would be:

1. Collect all load/discovery failures for a mount or page.
2. Continue rendering anything that is still valid when possible.
3. Show a compact error summary near the top of the page, or a dedicated error page if nothing can be rendered.
4. Include, for each failed entity or relationship:
   - the entity name
   - the field/navigation name, when known
   - the reason it could not be loaded
   - a short hint such as “not supported yet” or “falls back to heuristic display”

That gives us a middle ground between two bad extremes:

- silent failure, where users do not know why something is missing
- hard failure, where one unsupported edge case breaks the whole UI

### Suggested visibility by edge case

| Unsupported case | Suggested visibility | Reason |
|---|---|---|
| Keyless entity | High | Users can reasonably expect read-only views or at least a clear explanation if the entity is omitted |
| Composite primary key | High | Common enough in some domains to deserve an explicit “not supported yet” message |
| Composite foreign key | High | If we do not support it, the user should know why the relationship is missing |
| Shadow FK / no navigation property | Medium to High | Frequent in legacy models; a specific message helps users understand why the attribute cannot be applied |
| Computed / unmapped display property | Low | Usually a configuration mistake; fallback is often enough |
| Missing attribute target property | Low | Safe fallback is usually better than a visible error |
| Duplicate labels | Low | Cosmetic issue; the link still points to the right row |
| Inheritance hierarchy label ambiguity | Medium | Probably worth warning about if the resolved display property is inconsistent |
| Large lookup set / performance risk | Low | More of a performance budget issue than a correctness issue |

### My current bias

- **Keyless entity:** if we can support it gracefully with a stable technical identity, that is ideal; otherwise render a clear omission/error report instead of failing silently.
- **Composite primary key:** hard reject for now, but report the reason clearly.
- **Shadow FK:** yes, this is likely frequent enough that we should think about a graceful fallback path or a secondary configuration path later.
- **Duplicate labels:** acceptable if it is only cosmetic.
- **Filtering and sorting:** yes, the resolved display label should continue to drive those behaviors too.

## Provisional answers from your notes

You already answered the open questions in a useful way, so I am recording the current direction here:

1. **Keyless entities:** prefer graceful support if we can derive a stable technical identity; otherwise show a clear error/omission report rather than pretending they do not exist.
2. **Composite keys:** keep as a hard reject for now.
3. **Shadow FKs:** likely frequent enough that we should consider a graceful fallback path or a second configuration path later; the current navigation-property attribute model may not be enough.
4. **Duplicate labels:** acceptable when cosmetic.
5. **Filtering and sorting:** yes, they should already follow the visible resolved label.

## Proposed next step

Once we agree on which unsupported cases deserve user-visible reporting versus quiet fallback, I can turn that into a short implementation plan and keep the rest explicitly out of scope.
