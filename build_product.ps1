# One-click build and packaging script for ApexPOS
param(
    [string]$Version = "2.15"
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$releaseDir = Join-Path $root "release\ApexPOS-v$Version-win-x64"
$zipPath = Join-Path $root "release\ApexPOS-v$Version-win-x64.zip"

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "       ApexPOS v$Version - Product Release Builder     " -ForegroundColor Green
Write-Host "======================================================" -ForegroundColor Cyan

# 1. Stop any running instance
Stop-Process -Name "ApexPOS" -Force -ErrorAction SilentlyContinue
Stop-Process -Name "ApexPOS.Wpf" -Force -ErrorAction SilentlyContinue

# Ensure directory exists
if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
}

# Find dotnet executable
$dotnetExe = if (Test-Path "C:\dotnet\dotnet.exe") { "C:\dotnet\dotnet.exe" } elseif (Get-Command dotnet -ErrorAction SilentlyContinue) { (Get-Command dotnet).Source } else { "dotnet" }
Write-Host "Using .NET SDK: $dotnetExe" -ForegroundColor Cyan

# 2. Publish Self-Contained Release
Write-Host "Publishing self-contained Win-x64 release (v$Version)..." -ForegroundColor Yellow
& $dotnetExe publish (Join-Path $root "ApexPOS.Wpf\ApexPOS.Wpf.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:Version=$Version `
    -o $releaseDir

# 3. Clean up PDB debug symbols from distribution folder
Get-ChildItem -Path $releaseDir -Filter "*.pdb" | Remove-Item -Force -ErrorAction SilentlyContinue

# 4. Copy app icon
if (Test-Path (Join-Path $root "ApexPOS.Wpf\app.ico")) {
    Copy-Item (Join-Path $root "ApexPOS.Wpf\app.ico") -Destination (Join-Path $releaseDir "app.ico") -Force
}

# 5. Create Zip Archive
Write-Host "Creating portable distribution ZIP package..." -ForegroundColor Yellow
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$releaseDir\*" -DestinationPath $zipPath -CompressionLevel Optimal

$zipItem = Get-Item $zipPath
$zipMb = [Math]::Round($zipItem.Length / 1MB, 2)

Write-Host ""
Write-Host "======================================================" -ForegroundColor Green
Write-Host "        [SUCCESS] Product Build v$Version Ready!      " -ForegroundColor Green
Write-Host "======================================================" -ForegroundColor Green
Write-Host "Portable Folder : $releaseDir" -ForegroundColor White
Write-Host "Zip Package     : $zipPath ($zipMb MB)" -ForegroundColor White
Write-Host "Ready to copy and run on any other laptop!" -ForegroundColor Cyan
