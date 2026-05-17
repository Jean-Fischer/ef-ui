# Sonar Scan Implementation Plan

> **REQUIRED SUB-SKILL:** Use the executing-plans skill to implement this plan task-by-task.

**Goal:** Add a repo-owned PowerShell SonarCloud scan workflow that runs `begin -> build -> test -> end`, waits for server-side processing, and writes local summary artifacts that Pi agents can read.

**Architecture:** Keep the implementation in a single repo script at `scripts/sonar-scan.ps1`, with small helper functions for command execution, task metadata parsing, API polling, issue filtering, and artifact generation. The script is the source of truth; documentation points humans to the script directly, while a future `mise` task can wrap it without duplicating logic.

**Tech Stack:** PowerShell 7+, .NET SDK, `dotnet-sonarscanner`, SonarCloud Web API, existing solution `EfUi.sln`

---

## Implementation Notes

- Authentication must come from `EF_UI_SONAR_TOKEN`.
- The first implementation should default to **reporting** Sonar quality results, not failing the process solely because the quality gate is red.
- Artifact output must stay **outside the repo**, under `%LOCALAPPDATA%\pi\ef-ui\sonar\`.
- The Markdown and JSON files are the agent-facing contract.
- Use small PowerShell functions so the script remains testable by inspection and easy to evolve later.
- Keep the first version YAGNI: no `mise.toml` changes yet unless the script is already working end-to-end.

## Task 1: Scaffold the PowerShell workflow

**Files:**
- Create: `scripts/sonar-scan.ps1`
- Test: run `pwsh -File scripts/sonar-scan.ps1 -WhatIf` only after a minimal dry-run mode or early validation path exists

**Step 1: Create the script file with parameters and strict mode**

Add a first script skeleton like:

```powershell
[CmdletBinding()]
param(
    [string]$Solution = "EfUi.sln",
    [string]$ProjectKey = "jean-fischer_ef-ui",
    [string]$Organization = "jean-fischer",
    [string]$SonarHostUrl = "https://sonarcloud.io",
    [string]$TokenEnvVar = "EF_UI_SONAR_TOKEN",
    [string]$OutputRoot = "$env:LOCALAPPDATA\pi\ef-ui\sonar",
    [int]$PollIntervalSeconds = 5,
    [int]$PollTimeoutSeconds = 300,
    [switch]$Strict
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
```

**Step 2: Add helper functions for consistent output**

Start with small functions only:

```powershell
function Write-Section($Message) {
    Write-Host "`n=== $Message ==="
}

function Require-Command($Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command not found: $Name"
    }
}

function Get-RequiredEnvironmentVariable($Name) {
    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Required environment variable missing: $Name"
    }
    return $value
}
```

**Step 3: Run a syntax check**

Run:

```bash
pwsh -NoProfile -Command "$null = [System.Management.Automation.PSParser]::Tokenize((Get-Content 'scripts/sonar-scan.ps1' -Raw), [ref]$null); 'OK'"
```

Expected: `OK`

**Step 4: Commit the scaffold**

```bash
git add scripts/sonar-scan.ps1
git commit -m "feat: scaffold Sonar scan script"
```

## Task 2: Add prerequisite validation and path setup

**Files:**
- Modify: `scripts/sonar-scan.ps1`

**Step 1: Validate required tools and environment early**

Add a startup block that verifies:

```powershell
Require-Command dotnet
$token = Get-RequiredEnvironmentVariable $TokenEnvVar

$sonarToolList = dotnet tool list --global
if ($sonarToolList -notmatch 'dotnet-sonarscanner') {
    throw 'Global tool not found: dotnet-sonarscanner'
}
```

**Step 2: Create the output directory and define artifact paths**

Add:

```powershell
$null = New-Item -ItemType Directory -Force -Path $OutputRoot
$summaryMarkdownPath = Join-Path $OutputRoot 'summary.md'
$summaryJsonPath = Join-Path $OutputRoot 'summary.json'
```

**Step 3: Add a small startup summary**

Print the important resolved values:

```powershell
Write-Section 'Configuration'
Write-Host "Solution: $Solution"
Write-Host "Project key: $ProjectKey"
Write-Host "Output root: $OutputRoot"
```

**Step 4: Verify the early-failure path**

Run with the token temporarily unset in the shell:

```bash
pwsh -NoProfile -File scripts/sonar-scan.ps1
```

Expected: fail immediately with a clear message about `EF_UI_SONAR_TOKEN`.

**Step 5: Commit**

```bash
git add scripts/sonar-scan.ps1
git commit -m "feat: validate Sonar scan prerequisites"
```

## Task 3: Implement command execution for begin, build, test, and end

**Files:**
- Modify: `scripts/sonar-scan.ps1`

**Step 1: Add a helper to run native commands and stop on failure**

```powershell
function Invoke-Step {
    param(
        [Parameter(Mandatory)] [string]$Label,
        [Parameter(Mandatory)] [string]$FilePath,
        [Parameter(Mandatory)] [string[]]$ArgumentList
    )

    Write-Section $Label
    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed with exit code $LASTEXITCODE"
    }
}
```

**Step 2: Wire up the four main commands**

Use `sonar.token` rather than `sonar.login`:

```powershell
Invoke-Step -Label 'Sonar begin' -FilePath 'dotnet' -ArgumentList @(
    'sonarscanner', 'begin',
    "/k:$ProjectKey",
    "/o:$Organization",
    "/d:sonar.host.url=$SonarHostUrl",
    "/d:sonar.token=$token"
)

Invoke-Step -Label 'Build' -FilePath 'dotnet' -ArgumentList @('build', $Solution)
Invoke-Step -Label 'Test' -FilePath 'dotnet' -ArgumentList @('test', $Solution)
Invoke-Step -Label 'Sonar end' -FilePath 'dotnet' -ArgumentList @(
    'sonarscanner', 'end',
    "/d:sonar.token=$token"
)
```

**Step 3: Protect `end` execution with `try/finally` if needed**

If `build` or `test` fails, decide whether to skip `end` or still attempt it. For v1, keep it simple: if `begin` succeeded, make a best-effort `end` call in `finally`, but do not hide the original error.

**Step 4: Run the script against the real solution**

Run:

```bash
pwsh -NoProfile -File scripts/sonar-scan.ps1
```

Expected: begin/build/test/end all execute, or a precise failure message indicates where the pipeline broke.

**Step 5: Commit**

```bash
git add scripts/sonar-scan.ps1
git commit -m "feat: run Sonar begin build test end workflow"
```

## Task 4: Parse scanner task metadata and poll analysis completion

**Files:**
- Modify: `scripts/sonar-scan.ps1`

**Step 1: Add metadata file discovery**

Look for the scanner task metadata after `end`. Keep the path discovery explicit and logged. Start with candidates such as:

```powershell
$candidateMetadataFiles = @(
    (Join-Path (Get-Location) '.sonarqube\out\.sonar\report-task.txt'),
    (Join-Path (Get-Location) '.sonarqube\out\report-task.txt'),
    (Join-Path (Get-Location) '.scannerwork\report-task.txt')
)
```

Add a function:

```powershell
function Get-SonarMetadataFile {
    param([string[]]$Candidates)
    foreach ($candidate in $Candidates) {
        if (Test-Path $candidate) { return $candidate }
    }
    throw 'Could not find Sonar scanner metadata file (report-task.txt).'
}
```

**Step 2: Parse `key=value` metadata content**

```powershell
function Read-KeyValueFile($Path) {
    $map = @{}
    foreach ($line in Get-Content $Path) {
        if ($line -match '^(?<key>[^=]+)=(?<value>.*)$') {
            $map[$Matches['key']] = $Matches['value']
        }
    }
    return $map
}
```

Expected fields:
- `projectKey`
- `dashboardUrl`
- `ceTaskId`
- `ceTaskUrl`

**Step 3: Poll CE task status until completion**

Add a helper like:

```powershell
function Invoke-SonarGet($Uri, $Token) {
    $headers = @{ Authorization = "Bearer $Token" }
    return Invoke-RestMethod -Method Get -Uri $Uri -Headers $headers
}

function Wait-ForCeTaskCompletion {
    param($CeTaskUrl, $Token, $PollIntervalSeconds, $PollTimeoutSeconds)

    $deadline = (Get-Date).AddSeconds($PollTimeoutSeconds)
    do {
        $response = Invoke-SonarGet -Uri $CeTaskUrl -Token $Token
        $status = $response.task.status
        if ($status -in @('SUCCESS', 'FAILED', 'CANCELED')) {
            return $response.task
        }
        Start-Sleep -Seconds $PollIntervalSeconds
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for CE task completion: $CeTaskUrl"
}
```

**Step 4: Verify the task object contains `analysisId` on success**

Run the full script and confirm a completed task returns an `analysisId`. Print the dashboard URL and CE task status.

**Step 5: Commit**

```bash
git add scripts/sonar-scan.ps1
git commit -m "feat: poll Sonar analysis completion"
```

## Task 5: Retrieve quality gate status and filtered top issues

**Files:**
- Modify: `scripts/sonar-scan.ps1`

**Step 1: Fetch the quality gate result**

Use the analysis-aware API call after CE success:

```powershell
$qualityGateUrl = "$SonarHostUrl/api/qualitygates/project_status?analysisId=$analysisId"
$qualityGate = Invoke-SonarGet -Uri $qualityGateUrl -Token $token
```

Capture at least:
- overall status
- failed conditions
- metric key
- actual value
- error threshold

**Step 2: Fetch issues relevant to agent remediation**

Start with issue queries that preserve the user requirement:

```powershell
$securityIssuesUrl = "$SonarHostUrl/api/issues/search?componentKeys=$ProjectKey&types=VULNERABILITY&ps=500"
$criticalIssuesUrl = "$SonarHostUrl/api/issues/search?componentKeys=$ProjectKey&severities=BLOCKER,CRITICAL&ps=500"
```

If API behavior or overlap requires it, fetch both lists and de-duplicate by issue key.

**Step 3: Normalize the issue shape for artifact output**

Map each issue to a lean object:

```powershell
[pscustomobject]@{
    key      = $issue.key
    severity = $issue.severity
    type     = $issue.type
    rule     = $issue.rule
    message  = $issue.message
    file     = $issue.component
    line     = $issue.line
    status   = $issue.status
    url      = if ($dashboardUrl) { $dashboardUrl } else { $null }
}
```

**Step 4: Verify the retrieved dataset is useful**

Run the script and confirm the console output includes:
- quality gate status
- count of failed conditions
- count of security issues
- count of blocker/critical issues

**Step 5: Commit**

```bash
git add scripts/sonar-scan.ps1
git commit -m "feat: retrieve Sonar quality gate and top issues"
```

## Task 6: Generate Markdown and JSON artifacts outside the repo

**Files:**
- Modify: `scripts/sonar-scan.ps1`

**Step 1: Build the JSON contract first**

Create a stable object like:

```powershell
$result = [pscustomobject]@{
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    projectKey     = $metadata.projectKey
    dashboardUrl   = $metadata.dashboardUrl
    qualityGate    = [pscustomobject]@{
        status     = $qualityGate.projectStatus.status
        conditions = @($qualityGate.projectStatus.conditions)
    }
    issues         = @($filteredIssues)
}
```

Write it with:

```powershell
$result | ConvertTo-Json -Depth 10 | Set-Content -Encoding UTF8 $summaryJsonPath
```

**Step 2: Build a readable Markdown summary**

Format a document that contains:
- scan summary
- quality gate status
- failed conditions table or bullet list
- security issues section
- blocker/critical issues section
- absolute path to the JSON file

Suggested structure:

```markdown
# Sonar Summary

- Status: ERROR
- Project: jean-fischer_ef-ui
- Dashboard: https://sonarcloud.io/...

## Failed Quality Gate Conditions
- coverage: actual 72.1, threshold 80

## Security Issues
- CRITICAL VULNERABILITY src/File.cs:42 — message

## Blocker/Critical Issues
- BLOCKER BUG src/Other.cs:10 — message
```

**Step 3: Print artifact locations at the end**

```powershell
Write-Host "Markdown summary: $summaryMarkdownPath"
Write-Host "JSON summary: $summaryJsonPath"
```

**Step 4: Verify files exist and contain the expected sections**

Run:

```bash
pwsh -NoProfile -File scripts/sonar-scan.ps1
```

Then verify manually:
- `%LOCALAPPDATA%\pi\ef-ui\sonar\summary.md` exists
- `%LOCALAPPDATA%\pi\ef-ui\sonar\summary.json` exists
- both files reflect the latest scan

**Step 5: Commit**

```bash
git add scripts/sonar-scan.ps1
git commit -m "feat: write local Sonar summary artifacts"
```

## Task 7: Document usage for humans and future agents

**Files:**
- Modify: `README.md:1-9`

**Step 1: Add a Sonar scan section to the README**

Extend the README with exact usage, for example:

```markdown
## Sonar scan

Set `EF_UI_SONAR_TOKEN` in your shell, then run:

```bash
pwsh -NoProfile -File scripts/sonar-scan.ps1
```

Artifacts are written to `%LOCALAPPDATA%\pi\ef-ui\sonar\`:
- `summary.md`
- `summary.json`
```

**Step 2: Mention that quality results are reported even when the gate is red**

Add one sentence explaining that the script writes the artifacts so local agents and humans can inspect the findings.

**Step 3: Verify the README instructions match reality**

Run the documented command exactly as written.

**Step 4: Commit**

```bash
git add README.md
git commit -m "docs: document Sonar scan workflow"
```

## Task 8: End-to-end verification before claiming completion

**Files:**
- Verify: `scripts/sonar-scan.ps1`
- Verify: `README.md`

**Step 1: Run the full workflow from a clean shell**

Run:

```bash
pwsh -NoProfile -File scripts/sonar-scan.ps1
```

Expected:
- begin/build/test/end succeed
- SonarCloud processing is polled successfully
- `summary.md` and `summary.json` are written outside the repo

**Step 2: Inspect the artifact contents**

Check that:
- quality gate status is visible
- failed conditions are listed when applicable
- security issues appear
- blocker/critical issues appear
- file paths and line numbers are included when available

**Step 3: Confirm repo cleanliness**

Run:

```bash
git status --short
```

Expected: only intended tracked changes in `scripts/` and `README.md`.

**Step 4: Run any fast regression command already standard for the repo**

Run:

```bash
dotnet test EfUi.sln
```

Expected: PASS

**Step 5: Final commit**

```bash
git add scripts/sonar-scan.ps1 README.md
git commit -m "feat: add local SonarCloud scan workflow"
```

## Handoff Notes

- Do not add artifact files to the repo.
- Do not add a `mise` task until the script works reliably end-to-end.
- If the SonarCloud issue API returns unexpected schema variations, preserve the raw keys that are useful for remediation rather than over-normalizing too early.
- If `report-task.txt` lands in a different path on this machine, update the metadata file search function and document the final resolved location in the commit.
