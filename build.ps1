<#
.SYNOPSIS
    Publishes VertiRPC and packages it into an Inno Setup installer.

.EXAMPLE
    .\build.ps1
    .\build.ps1 -SkipInstaller     # just the publish output
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$project = Join-Path $root "src\VertiRPC\VertiRPC.csproj"
$publishDir = Join-Path $root "publish"

# The version lives in the csproj and nowhere else; everything downstream reads
# it from there so a release never ships two different numbers.
[xml]$csproj = Get-Content $project
$version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "No <Version> found in $project" }
Write-Host "VertiRPC $version" -ForegroundColor Cyan

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

Write-Host "Running tests..." -ForegroundColor Cyan
dotnet test (Join-Path $root "tests\VertiRPC.Tests\VertiRPC.Tests.csproj") -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "Tests failed" }

Write-Host "Publishing..." -ForegroundColor Cyan
# Framework-dependent: a ~1 MB installer, with the runtime check in the .iss.
dotnet publish $project -c $Configuration -r $Runtime --self-contained false -o $publishDir --nologo
if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

if ($SkipInstaller) {
    Write-Host "Published to $publishDir" -ForegroundColor Green
    return
}

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Warning "Inno Setup 6 not found. Install it (winget install JRSoftware.InnoSetup) or pass -SkipInstaller."
    return
}

Write-Host "Building installer..." -ForegroundColor Cyan
& $iscc "/DAppVersion=$version" "/DPublishDir=$publishDir" (Join-Path $root "installer\VertiRPC.iss")
if ($LASTEXITCODE -ne 0) { throw "Installer build failed" }

Write-Host "Installer: $(Join-Path $root "installer\Output\VertiRPC-$version-Setup.exe")" -ForegroundColor Green
