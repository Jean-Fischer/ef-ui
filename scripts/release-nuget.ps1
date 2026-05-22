param(
    [string]$PackageVersion = $env:PACKAGE_VERSION,
    [string]$NuGetApiKey = $env:NUGET_API_KEY
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($PackageVersion)) {
    throw 'PACKAGE_VERSION is required.'
}

if ([string]::IsNullOrWhiteSpace($NuGetApiKey)) {
    throw 'NUGET_API_KEY is required.'
}

$artifactsDir = Join-Path (Get-Location) '.artifacts/nuget'
New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null

& dotnet pack src/EfUi.AspNetCore/EfUi.AspNetCore.csproj `
    -c Release `
    --no-build `
    --no-restore `
    -o $artifactsDir `
    -p:ContinuousIntegrationBuild=true `
    -p:PackageVersion=$PackageVersion

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$nupkg = Join-Path $artifactsDir "EfUi.AspNetCore.$PackageVersion.nupkg"
& dotnet nuget push $nupkg `
    --api-key $NuGetApiKey `
    --source https://api.nuget.org/v3/index.json `
    --skip-duplicate

exit $LASTEXITCODE
