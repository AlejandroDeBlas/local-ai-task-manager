param (
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$projectPath = Join-Path $rootDir "src\LocalAITaskManager.App\LocalAITaskManager.App.csproj"
$outputDir = Join-Path $rootDir "artifacts\publish\win-x64"

Write-Host "Publishing Local AI Task Manager (win-x64, self-contained, single-file)..." -ForegroundColor Cyan

if (Test-Path $outputDir) {
    Remove-Item -Path $outputDir -Recurse -Force | Out-Null
}

$publishArgs = @(
    "publish",
    $projectPath,
    "-c", $Configuration,
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:PublishTrimmed=false",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-o", $outputDir
)

& dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "Publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "`nSuccessfully published to: $outputDir" -ForegroundColor Green
Get-ChildItem -Path $outputDir | Select-Object Name, Length, LastWriteTime
