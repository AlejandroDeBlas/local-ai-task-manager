param (
    [string]$Version = "0.1.0",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$solutionPath = Join-Path $rootDir "LocalAITaskManager.sln"
$publishScript = Join-Path $scriptDir "publish.ps1"
$publishDir = Join-Path $rootDir "artifacts\publish\win-x64"
$releaseDir = Join-Path $rootDir "artifacts\release\v$Version"
$stagingDir = Join-Path $rootDir "artifacts\staging\v$Version"
$zipName = "LocalAITaskManager-v$Version-win-x64.zip"
$zipPath = Join-Path $releaseDir $zipName
$shaPath = Join-Path $releaseDir "$zipName.sha256"
$manifestPath = Join-Path $releaseDir "release-manifest.json"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Local AI Task Manager - Release Packaging v$Version" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Validate Solution File
if (-not (Test-Path $solutionPath)) {
    throw "Solution file not found at: $solutionPath"
}

# 2. Restore Dependencies
Write-Host "`n[1/7] Restoring dependencies..." -ForegroundColor Yellow
& dotnet restore $solutionPath
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed" }

# 3. Build Solution
Write-Host "`n[2/7] Building solution ($Configuration)..." -ForegroundColor Yellow
& dotnet build $solutionPath -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

# 4. Run Tests
Write-Host "`n[3/7] Running test suite ($Configuration)..." -ForegroundColor Yellow
& dotnet test $solutionPath -c $Configuration --no-build --verbosity normal
if ($LASTEXITCODE -ne 0) { throw "dotnet test failed" }

# 5. Verify Code Formatting
Write-Host "`n[4/7] Verifying code formatting..." -ForegroundColor Yellow
& dotnet format $solutionPath --verify-no-changes
if ($LASTEXITCODE -ne 0) { throw "dotnet format verification failed" }

# 6. Publish Single-File Executable
Write-Host "`n[5/7] Publishing self-contained single file..." -ForegroundColor Yellow
& powershell -ExecutionPolicy Bypass -File $publishScript -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { throw "publish script failed" }

$exePath = Join-Path $publishDir "LocalAITaskManager.exe"
if (-not (Test-Path $exePath)) {
    throw "Published executable not found at: $exePath"
}

# 7. Stage and Package Release
Write-Host "`n[6/7] Staging release files..." -ForegroundColor Yellow

if (Test-Path $stagingDir) {
    Remove-Item -Path $stagingDir -Recurse -Force | Out-Null
}
New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
}

# Copy EXE
Copy-Item -Path $exePath -Destination $stagingDir -Force

# Copy LICENSE
$licenseSrc = Join-Path $rootDir "LICENSE"
if (Test-Path $licenseSrc) {
    Copy-Item -Path $licenseSrc -Destination $stagingDir -Force
}

# Create README.txt quickstart for ZIP
$readmeTemplatePath = Join-Path $scriptDir "README.txt.template"
$readmeTxtPath = Join-Path $stagingDir "README.txt"
if (Test-Path $readmeTemplatePath) {
    $readmeContent = Get-Content -Path $readmeTemplatePath -Raw -Encoding utf8
    $readmeContent = $readmeContent.Replace("{{VERSION}}", $Version)
    Set-Content -Path $readmeTxtPath -Value $readmeContent -Encoding utf8
}

# Create ZIP archive
Write-Host "`n[7/7] Creating ZIP package and checksums..." -ForegroundColor Yellow
if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath -Force | Out-Null
}
Compress-Archive -Path "$stagingDir\*" -DestinationPath $zipPath -Force

# Compute SHA256
$hash = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path $shaPath -Value "$hash  $zipName" -Encoding utf8

# Generate release manifest
$manifest = [ordered]@{
    version       = $Version
    runtime       = "win-x64"
    artifact      = $zipName
    sha256        = $hash
    selfContained = $true
    singleFile    = $true
}
$manifestJson = $manifest | ConvertTo-Json -Depth 4
Set-Content -Path $manifestPath -Value $manifestJson -Encoding utf8

# Cleanup staging
Remove-Item -Path $stagingDir -Recurse -Force | Out-Null

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host " Release v$Version successfully staged!" -ForegroundColor Green
Write-Host " Output Directory: $releaseDir" -ForegroundColor Green
Write-Host " ZIP Package:      $zipPath" -ForegroundColor Green
Write-Host " SHA256 Checksum:  $hash" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green

Get-ChildItem -Path $releaseDir | Select-Object Name, Length, LastWriteTime
