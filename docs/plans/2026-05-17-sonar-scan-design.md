# Sonar Scan Integration Design

**Date:** 2026-05-17

## Objective

Add a repo-owned SonarCloud scanning workflow that Pi agents can run at the end of their work to verify code quality, then consume the results locally from generated artifact files.

The first version should fit the current .NET-heavy repository, use the already-installed `dotnet-sonarscanner` tool, authenticate through `EF_UI_SONAR_TOKEN`, and remain simple enough for both humans and agents to run reliably.

## Validated Decisions

- Use a **repo script as the source of truth** rather than embedding all behavior directly in `mise.toml`.
- Implement the first version as a **PowerShell script**.
- Prefer a **simple default mode** that reports results clearly instead of aggressively failing on quality gate issues.
- Run Sonar around **build + test** in the first version.
- Generate **local artifact files outside the repository** so agents can read them without any risk of accidental commits.
- Include both **quality gate failures** and a curated list of **top issues**, especially:
  - all security-related findings
  - all high-severity findings such as `CRITICAL` and `BLOCKER`
- Keep room for a later **`mise` wrapper** once the script behavior is stable.

## Evaluated Approaches

### Option A — `mise` task only

Put the entire workflow directly in `mise.toml`.

**Pros**
- small command surface
- convenient for developers already using `mise`

**Cons**
- harder to evolve once polling, API calls, filtering, and richer artifact generation are added
- less reusable outside `mise`
- more awkward to test incrementally

### Option B — Repo script with optional `mise` wrapper (**recommended**)

Create a PowerShell script in the repo and optionally expose it later via `mise run sonar`.

**Pros**
- repo-owned behavior is explicit and versioned
- easy for Pi agents, developers, and future CI to call the same implementation
- better place for polling, API parsing, and artifact generation
- keeps `mise` as a convenience layer rather than the implementation layer

**Cons**
- one extra file to maintain

### Option C — Pi-specific workflow only

Teach agents to run a sequence of Sonar commands without a repo script.

**Pros**
- minimal repo changes initially

**Cons**
- brittle and harder to reuse
- no stable contract for humans or CI
- harder to evolve safely

## Recommended Repository Shape

```text
scripts/
  sonar-scan.ps1

docs/plans/
  2026-05-17-sonar-scan-design.md
```

Future optional addition:

```text
mise.toml
```

with a thin wrapper such as:

```toml
[tasks.sonar]
run = "pwsh -File scripts/sonar-scan.ps1"
```

## Primary Workflow

The script should orchestrate the following sequence:

1. Validate prerequisites.
2. Start Sonar analysis.
3. Build the solution.
4. Run tests.
5. Finish Sonar analysis and upload the results.
6. Read scanner task metadata.
7. Poll SonarCloud until server-side processing completes.
8. Fetch quality gate and issue data.
9. Write local artifacts for humans and agents.

### High-Level Command Flow

```powershell
dotnet sonarscanner begin /k:"jean-fischer_ef-ui" /o:"jean-fischer" /d:sonar.host.url="https://sonarcloud.io" /d:sonar.token="$env:EF_UI_SONAR_TOKEN"
dotnet build EfUi.sln
dotnet test EfUi.sln
dotnet sonarscanner end /d:sonar.token="$env:EF_UI_SONAR_TOKEN"
```

## Why Local Scanner Output Is Not Enough

The scanner gives useful execution logs and writes task metadata locally, but the full answer to **“what is wrong with this code?”** becomes reliable only after SonarCloud processes the uploaded analysis.

Therefore, the design separates:

- **local submission phase**: scanner begin/build/test/end
- **remote result retrieval phase**: SonarCloud API polling and issue retrieval

This is the key design choice that enables Pi agents to inspect a stable local summary while still benefiting from SonarCloud’s processed analysis.

## Result Retrieval Strategy

After the `end` step, the script should locate the scanner-generated metadata file and extract at least:

- `projectKey`
- `dashboardUrl`
- `ceTaskId`
- `ceTaskUrl`

The script should then:

1. poll the CE task endpoint until processing finishes
2. extract the `analysisId` from the CE task result
3. fetch quality gate status for that analysis
4. fetch filtered issues for the analyzed project

### Intended SonarCloud Data Sources

The exact request details can be finalized during implementation, but the script is expected to use SonarCloud Web API endpoints for:

- CE task status
- quality gate status
- issue search

This keeps the logic portable and avoids scraping HTML pages.

## Artifact Strategy

Artifacts should be written **outside the repository** so they remain readable by local agents without any risk of being committed.

### Default Output Directory

```text
%LOCALAPPDATA%\pi\ef-ui\sonar\
```

### Expected Files

```text
%LOCALAPPDATA%\pi\ef-ui\sonar\summary.md
%LOCALAPPDATA%\pi\ef-ui\sonar\summary.json
```

### Why Two Formats

- **Markdown** is optimized for fast human and agent inspection.
- **JSON** is optimized for automation and future Pi workflows.

## Markdown Artifact Contract

The Markdown summary should be short, stable, and action-oriented.

Suggested sections:

1. **Scan summary**
   - timestamp
   - project key
   - branch if available
   - dashboard URL
   - overall quality gate status

2. **Failed quality gate conditions**
   - metric name
   - actual value
   - threshold
   - status

3. **Top issues found**
   - grouped or filtered for:
     - security-related findings
     - `BLOCKER`
     - `CRITICAL`
   - each issue should include:
     - severity
     - type
     - rule key
     - message
     - file path
     - line number

4. **Recommended remediation order**
   - security issues first
   - then blocker/critical issues
   - rerun scan after fixes

## JSON Artifact Contract

The JSON output should contain stable machine-readable fields such as:

```json
{
  "projectKey": "jean-fischer_ef-ui",
  "dashboardUrl": "https://sonarcloud.io/...",
  "qualityGate": {
    "status": "OK",
    "conditions": []
  },
  "issues": [
    {
      "severity": "CRITICAL",
      "type": "VULNERABILITY",
      "rule": "...",
      "message": "...",
      "file": "src/...",
      "line": 42
    }
  ]
}
```

The exact property set may evolve, but the first version should favor clarity over completeness.

## Issue Filtering Rules for v1

The script should retrieve and retain at least:

- all **security-related findings**
  - initial target: vulnerabilities and other security-relevant issue types available from the API
- all **`BLOCKER`** findings
- all **`CRITICAL`** findings

If the response volume is high, the script may cap non-security issue counts in the Markdown file while preserving the full filtered list in JSON.

## Execution Behavior

### Hard failures

The script should return a non-zero exit code when:

- `dotnet` is unavailable
- `dotnet sonarscanner` is unavailable
- `EF_UI_SONAR_TOKEN` is missing
- begin/build/test/end command execution fails
- required metadata cannot be found
- SonarCloud API polling fails unexpectedly

### Soft failures

In the default first version, the script should **not necessarily fail the process** solely because the quality gate is red.

Instead it should:

- print a clear terminal summary
- write the local artifacts
- make the failing conditions obvious

This keeps local usage practical while still giving Pi agents enough evidence to decide that work is not actually done.

### Optional future strict mode

If implementation remains simple, a later `-Strict` switch may cause the script to return a non-zero exit code when:

- the quality gate status is failing, or
- filtered top issues are present above the configured threshold

## Agent Consumption Model

A future Pi agent should be able to:

1. run the script
2. read `%LOCALAPPDATA%\pi\ef-ui\sonar\summary.md` or `summary.json`
3. answer:
   - whether quality checks passed
   - which files and lines are problematic
   - whether security issues exist
   - what should be fixed before claiming completion

This means the file output is the integration contract, not terminal logs alone.

## Future `mise` Integration

Once the script is stable, `mise` should expose it with a thin wrapper. That gives:

- a memorable command for humans
- a stable entrypoint for agents
- a path toward broader tooling standardization in the repo

Recommended future command shape:

```bash
mise run sonar
```

with optional variants later such as:

```bash
mise run sonar -- --strict
```

## Future Enhancements

- add test coverage import if desired
- add branch/PR-specific filtering
- include Security Hotspots if the API path and permissions are straightforward
- add `-Strict` mode
- integrate with CI once the local contract is proven
- optionally store the latest scan timestamp and cleanup policy for old artifacts

## Acceptance Criteria

The design is considered successfully implemented when:

- a developer or Pi agent can run one repo-owned command to perform a Sonar scan
- authentication comes from `EF_UI_SONAR_TOKEN`
- the workflow covers `begin -> build -> test -> end`
- the script waits for SonarCloud processing to complete
- the script retrieves quality gate status
- the script retrieves top issues including security and blocker/critical items
- the script writes a readable Markdown summary outside the repo
- the script writes a machine-readable JSON summary outside the repo
- agents can use those files as evidence when deciding whether generated code is acceptable
