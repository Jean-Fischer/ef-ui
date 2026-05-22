# Publishing EfUi.AspNetCore

This repository publishes a single NuGet package: `EfUi.AspNetCore`.

## Manual release flow

1. Open the GitHub Actions **Publish NuGet package** workflow.
2. Enter the exact package version you want to ship.
3. Run the workflow.
4. After a successful publish, create a git tag that matches the package version.

## Version and tag convention

Use the same value for the package version and the git tag, with a leading `v` in the tag name.

Examples:

- package version: `1.2.3`
- git tag: `v1.2.3`

For prereleases, keep the same rule:

- package version: `1.2.3-preview.1`
- git tag: `v1.2.3-preview.1`

## Release note template

```md
# EfUi.AspNetCore v1.2.3

## Highlights
- 

## Breaking changes
- None

## Notes
- Targets .NET 8+
- Validated against EF Core 8.x
- Works with any EF Core provider
```

## Required GitHub secret

The workflow expects:

- `NUGET_API_KEY`
