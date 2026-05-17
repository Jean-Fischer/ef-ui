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

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solutionPath = if ([System.IO.Path]::IsPathRooted($Solution)) { $Solution } else { Join-Path $repoRoot $Solution }

function Write-Section {
    param(
        [Parameter(Mandatory)]
        [string]$Message
    )

    Write-Host "=== $Message ==="
}

function Require-Command {
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command not found: $Name"
    }
}

function Get-RequiredEnvironmentVariable {
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Required environment variable missing: $Name"
    }

    return $value
}

function Invoke-Step {
    param(
        [Parameter(Mandatory)]
        [string]$Label,

        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$ArgumentList
    )

    Write-Section $Label
    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "$Label failed with exit code $LASTEXITCODE"
    }
}

function Get-SonarMetadataFile {
    param(
        [Parameter(Mandatory)]
        [string[]]$Candidates,

        [datetime]$NotOlderThan
    )

    foreach ($candidate in $Candidates) {
        if (-not (Test-Path -LiteralPath $candidate)) {
            continue
        }

        if ($PSBoundParameters.ContainsKey('NotOlderThan')) {
            $item = Get-Item -LiteralPath $candidate
            if ($item.LastWriteTime -lt $NotOlderThan) {
                continue
            }
        }

        return $candidate
    }

    throw 'Could not find Sonar scanner metadata file (report-task.txt).'
}

function Read-KeyValueFile {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $map = @{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        if ($line -match '^(?<key>[^=]+)=(?<value>.*)$') {
            $map[$Matches['key']] = $Matches['value']
        }
    }

    return $map
}

function Invoke-SonarGet {
    param(
        [Parameter(Mandatory)]
        [string]$Uri,

        [Parameter(Mandatory)]
        [string]$Token
    )

    $headers = @{ Authorization = "Bearer $Token" }
    return Invoke-RestMethod -Method Get -Uri $Uri -Headers $headers
}

function Wait-ForCeTaskCompletion {
    param(
        [Parameter(Mandatory)]
        [string]$CeTaskUrl,

        [Parameter(Mandatory)]
        [string]$Token,

        [Parameter(Mandatory)]
        [int]$PollIntervalSeconds,

        [Parameter(Mandatory)]
        [int]$PollTimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($PollTimeoutSeconds)
    while ($true) {
        $response = Invoke-SonarGet -Uri $CeTaskUrl -Token $Token
        $task = $response.task
        if ($null -eq $task) {
            throw "Sonar CE task response did not include task details: $CeTaskUrl"
        }

        Write-Host "CE task status: $($task.status)"
        if ($task.status -in @('SUCCESS', 'FAILED', 'CANCELED')) {
            return $task
        }

        $remainingSeconds = [Math]::Ceiling(($deadline - (Get-Date)).TotalSeconds)
        if ($remainingSeconds -le 0) {
            break
        }

        $sleepSeconds = [Math]::Min($PollIntervalSeconds, $remainingSeconds)
        Start-Sleep -Seconds $sleepSeconds
    }

    throw "Timed out waiting for CE task completion: $CeTaskUrl"
}

function Get-ObjectPropertyValue {
    param(
        [Parameter(Mandatory)]
        [psobject]$Object,

        [Parameter(Mandatory)]
        [string]$Name
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Normalize-SonarIssue {
    param(
        [Parameter(Mandatory)]
        [psobject]$Issue
    )

    $file = Get-ObjectPropertyValue -Object $Issue -Name 'component'
    if (-not [string]::IsNullOrWhiteSpace($file) -and $file.Contains(':')) {
        $file = ($file -split ':', 2)[1]
    }

    $impacts = @(Get-ObjectPropertyValue -Object $Issue -Name 'impacts')
    $impactSeverities = @($impacts | ForEach-Object { Get-ObjectPropertyValue -Object $_ -Name 'severity' } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
    $impactQualities = @($impacts | ForEach-Object { Get-ObjectPropertyValue -Object $_ -Name 'softwareQuality' } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
    $primaryImpactSeverity = if ($impactSeverities.Count -gt 0) { $impactSeverities[0] } else { $null }

    return [pscustomobject]@{
        key                     = Get-ObjectPropertyValue -Object $Issue -Name 'key'
        severity                = Get-ObjectPropertyValue -Object $Issue -Name 'severity'
        impactSeverity          = $primaryImpactSeverity
        impactSeverities        = $impactSeverities
        impactSoftwareQualities = $impactQualities
        type                    = Get-ObjectPropertyValue -Object $Issue -Name 'type'
        rule                    = Get-ObjectPropertyValue -Object $Issue -Name 'rule'
        message                 = Get-ObjectPropertyValue -Object $Issue -Name 'message'
        file                    = $file
        line                    = Get-ObjectPropertyValue -Object $Issue -Name 'line'
        status                  = Get-ObjectPropertyValue -Object $Issue -Name 'status'
    }
}

function Get-SonarIssues {
    param(
        [Parameter(Mandatory)]
        [string]$BaseUrl,

        [Parameter(Mandatory)]
        [string]$Token,

        [Parameter(Mandatory)]
        [string]$ProjectKey,

        [string]$Types,
        [string]$Severities,
        [string]$ImpactSeverities,
        [string]$ImpactSoftwareQualities
    )

    $pageSize = 500
    $allIssues = @()
    $page = 1

    do {
        $queryParts = @(
            "componentKeys=$([uri]::EscapeDataString($ProjectKey))",
            'resolved=false',
            "ps=$pageSize",
            "p=$page"
        )

        if (-not [string]::IsNullOrWhiteSpace($Types)) {
            $queryParts += "types=$([uri]::EscapeDataString($Types))"
        }

        if (-not [string]::IsNullOrWhiteSpace($Severities)) {
            $queryParts += "severities=$([uri]::EscapeDataString($Severities))"
        }

        if (-not [string]::IsNullOrWhiteSpace($ImpactSeverities)) {
            $queryParts += "impactSeverities=$([uri]::EscapeDataString($ImpactSeverities))"
        }

        if (-not [string]::IsNullOrWhiteSpace($ImpactSoftwareQualities)) {
            $queryParts += "impactSoftwareQualities=$([uri]::EscapeDataString($ImpactSoftwareQualities))"
        }

        $issuesUrl = "$BaseUrl/api/issues/search?$($queryParts -join '&')"
        $response = Invoke-SonarGet -Uri $issuesUrl -Token $Token
        $issues = @($response.issues)
        $allIssues += $issues

        $paging = $response.paging
        $total = if ($null -ne $paging -and $null -ne $paging.total) { [int]$paging.total } else { $allIssues.Count }
        $page += 1
    } while ($allIssues.Count -lt $total -and $issues.Count -gt 0)

    return $allIssues
}

$metadataFileCandidates = @(
    (Join-Path $repoRoot '.sonarqube\out\.sonar\report-task.txt'),
    (Join-Path $repoRoot '.sonarqube\out\report-task.txt'),
    (Join-Path $repoRoot '.scannerwork\report-task.txt')
)

Require-Command 'dotnet'
$token = Get-RequiredEnvironmentVariable -Name $TokenEnvVar

$globalTools = & dotnet tool list --global
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to list global dotnet tools.'
}

if (($globalTools | Out-String) -notmatch '(^|\s)dotnet-sonarscanner(\s|$)') {
    throw 'Global tool not found: dotnet-sonarscanner'
}

$null = New-Item -ItemType Directory -Force -Path $OutputRoot
$summaryMarkdownPath = Join-Path $OutputRoot 'summary.md'
$summaryJsonPath = Join-Path $OutputRoot 'summary.json'

Write-Section 'Configuration'
Write-Host "Repository root: $repoRoot"
Write-Host "Solution: $solutionPath"
Write-Host "Project key: $ProjectKey"
Write-Host "Output root: $OutputRoot"

$beginSucceeded = $false
$endAttempted = $false
$scanStartTime = Get-Date

foreach ($metadataCandidate in $metadataFileCandidates) {
    if (Test-Path -LiteralPath $metadataCandidate) {
        Remove-Item -LiteralPath $metadataCandidate -Force
    }
}

try {
    Invoke-Step -Label 'Sonar begin' -FilePath 'dotnet' -ArgumentList @(
        'sonarscanner', 'begin',
        "/k:$ProjectKey",
        "/o:$Organization",
        "/d:sonar.host.url=$SonarHostUrl",
        "/d:sonar.token=$token"
    )
    $beginSucceeded = $true

    Invoke-Step -Label 'Build' -FilePath 'dotnet' -ArgumentList @(
        'build',
        $solutionPath
    )

    Invoke-Step -Label 'Test' -FilePath 'dotnet' -ArgumentList @(
        'test',
        $solutionPath
    )

    $endAttempted = $true
    Invoke-Step -Label 'Sonar end' -FilePath 'dotnet' -ArgumentList @(
        'sonarscanner', 'end',
        "/d:sonar.token=$token"
    )
}
finally {
    if ($beginSucceeded -and -not $endAttempted) {
        try {
            Invoke-Step -Label 'Sonar end (best effort)' -FilePath 'dotnet' -ArgumentList @(
                'sonarscanner', 'end',
                "/d:sonar.token=$token"
            )
        }
        catch {
            Write-Warning "Best-effort Sonar end failed: $($_.Exception.Message)"
        }
    }
}

Write-Section 'Sonar result retrieval'
$metadataFile = Get-SonarMetadataFile -Candidates $metadataFileCandidates -NotOlderThan $scanStartTime
Write-Host "Metadata file: $metadataFile"

$metadata = Read-KeyValueFile -Path $metadataFile
$ceTaskUrl = $metadata['ceTaskUrl']
if ([string]::IsNullOrWhiteSpace($ceTaskUrl)) {
    throw 'Sonar metadata did not include ceTaskUrl.'
}

$ceTask = Wait-ForCeTaskCompletion -CeTaskUrl $ceTaskUrl -Token $token -PollIntervalSeconds $PollIntervalSeconds -PollTimeoutSeconds $PollTimeoutSeconds
if ($ceTask.status -ne 'SUCCESS') {
    throw "Sonar CE task finished with status $($ceTask.status)."
}

$analysisId = $ceTask.analysisId
if ([string]::IsNullOrWhiteSpace($analysisId)) {
    throw 'Sonar CE task completed without an analysisId.'
}

$resolvedProjectKey = if ([string]::IsNullOrWhiteSpace($metadata['projectKey'])) { $ProjectKey } else { $metadata['projectKey'] }
$qualityGateUrl = "$SonarHostUrl/api/qualitygates/project_status?analysisId=$([uri]::EscapeDataString($analysisId))"
$qualityGate = Invoke-SonarGet -Uri $qualityGateUrl -Token $token

$securityIssuesRaw = Get-SonarIssues -BaseUrl $SonarHostUrl -Token $token -ProjectKey $resolvedProjectKey -ImpactSoftwareQualities 'SECURITY'
$highImpactIssuesRaw = Get-SonarIssues -BaseUrl $SonarHostUrl -Token $token -ProjectKey $resolvedProjectKey -ImpactSeverities 'HIGH'

$filteredIssuesByKey = @{}
foreach ($issue in @($securityIssuesRaw) + @($highImpactIssuesRaw)) {
    if ($null -eq $issue -or [string]::IsNullOrWhiteSpace($issue.key)) {
        continue
    }

    $filteredIssuesByKey[$issue.key] = Normalize-SonarIssue -Issue $issue
}

$securityIssues = @($securityIssuesRaw | ForEach-Object { Normalize-SonarIssue -Issue $_ } | Sort-Object key -Unique)
$highImpactIssues = @($highImpactIssuesRaw | ForEach-Object { Normalize-SonarIssue -Issue $_ } | Sort-Object key -Unique)
$filteredIssues = @($filteredIssuesByKey.Values | Sort-Object key)
$qualityGateConditions = @(
    $qualityGate.projectStatus.conditions | ForEach-Object {
        [pscustomobject]@{
            metricKey      = $_.metricKey
            comparator     = $_.comparator
            status         = $_.status
            actualValue    = $_.actualValue
            errorThreshold = $_.errorThreshold
        }
    }
)
$failedConditions = @($qualityGateConditions | Where-Object { $_.status -ne 'OK' })
$dashboardUrl = if ([string]::IsNullOrWhiteSpace($metadata['dashboardUrl'])) { $null } else { $metadata['dashboardUrl'] }

$result = [pscustomobject]@{
    generatedAtUtc = [DateTime]::UtcNow.ToString('o')
    projectKey     = $resolvedProjectKey
    dashboardUrl   = $dashboardUrl
    qualityGate    = [pscustomobject]@{
        status     = $qualityGate.projectStatus.status
        conditions = $qualityGateConditions
    }
    issues         = $filteredIssues
}

$result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $summaryJsonPath -Encoding UTF8

$summaryLines = [System.Collections.Generic.List[string]]::new()
$summaryLines.Add('# Sonar Summary')
$summaryLines.Add('')
$summaryLines.Add('## Scan Summary')
$summaryLines.Add(('- Generated (UTC): {0}' -f $result.generatedAtUtc))
$summaryLines.Add(('- Project: {0}' -f $result.projectKey))
$summaryLines.Add(('- Quality Gate: {0}' -f $result.qualityGate.status))
$summaryLines.Add(('- Dashboard: {0}' -f ($(if ([string]::IsNullOrWhiteSpace($dashboardUrl)) { 'n/a' } else { $dashboardUrl }))))
$summaryLines.Add(('- JSON Artifact: {0}' -f [System.IO.Path]::GetFullPath($summaryJsonPath)))
$summaryLines.Add('')
$summaryLines.Add('## Failed Quality Gate Conditions')
if ($failedConditions.Count -eq 0) {
    $summaryLines.Add('- None')
}
else {
    foreach ($condition in $failedConditions) {
        $summaryLines.Add(('- {0}: status {1}, actual {2}, threshold {3}' -f $condition.metricKey, $condition.status, $condition.actualValue, $condition.errorThreshold))
    }
}
$summaryLines.Add('')
$summaryLines.Add('## Security Findings')
if ($securityIssues.Count -eq 0) {
    $summaryLines.Add('- None')
}
else {
    foreach ($issue in $securityIssues) {
        $location = if ($null -ne $issue.line) { '{0}:{1}' -f $issue.file, $issue.line } else { $issue.file }
        $impactLabel = if ([string]::IsNullOrWhiteSpace($issue.impactSeverity)) { 'n/a' } else { $issue.impactSeverity }
        $summaryLines.Add(('- {0} {1} {2} — {3}' -f $impactLabel, $issue.type, $location, $issue.message))
    }
}
$summaryLines.Add('')
$summaryLines.Add('## High-Impact Findings')
if ($highImpactIssues.Count -eq 0) {
    $summaryLines.Add('- None')
}
else {
    foreach ($issue in $highImpactIssues) {
        $location = if ($null -ne $issue.line) { '{0}:{1}' -f $issue.file, $issue.line } else { $issue.file }
        $impactLabel = if ([string]::IsNullOrWhiteSpace($issue.impactSeverity)) { 'n/a' } else { $issue.impactSeverity }
        $summaryLines.Add(('- {0} {1} {2} — {3}' -f $impactLabel, $issue.type, $location, $issue.message))
    }
}

Set-Content -LiteralPath $summaryMarkdownPath -Encoding UTF8 -Value $summaryLines

Write-Host "Quality gate status: $($qualityGate.projectStatus.status)"
Write-Host "Failed quality gate conditions: $($failedConditions.Count)"
Write-Host "Security finding count: $($securityIssues.Count)"
Write-Host "High-impact finding count: $($highImpactIssues.Count)"
Write-Host "Filtered issue count: $($filteredIssues.Count)"
Write-Host "Markdown summary: $([System.IO.Path]::GetFullPath($summaryMarkdownPath))"
Write-Host "JSON summary: $([System.IO.Path]::GetFullPath($summaryJsonPath))"

if ($Strict -and $result.qualityGate.status -ne 'OK') {
    throw "Quality gate failed with status $($result.qualityGate.status)."
}
