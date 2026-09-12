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

# A copy running out of this repo holds its DLLs open, which would fail the
# clean below. An installed copy anywhere else is left alone.
$ours = Get-Process VertiRPC -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) }

foreach ($instance in $ours) {
    Write-Host "Stopping $($instance.Path) (pid $($instance.Id))" -ForegroundColor Yellow
    Stop-Process -Id $instance.Id -Force
}
if ($ours) { $ours | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue }

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

Write-Host "Running tests..." -ForegroundColor Cyan
dotnet test (Join-Path $root "tests\VertiRPC.Tests\VertiRPC.Tests.csproj") -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "Tests failed" }

Write-Host "Publishing..." -ForegroundColor Cyan
# Framework-dependent: a ~3 MB installer, with the runtime check in the .iss.
dotnet publish $project -c $Configuration -r $Runtime --self-contained false -o $publishDir --nologo
if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

if ($SkipInstaller) {
    Write-Host "Published to $publishDir" -ForegroundColor Green
    return
}

# Inno Setup installs per machine or per user (winget picks the latter, which
# needs no elevation), and its folder carries the major version, so probe both
# scopes and wildcard the version rather than pinning one path. The runners have
# it on PATH, which is why that comes first.
$iscc = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source,
    "${env:ProgramFiles(x86)}\Inno Setup *\ISCC.exe",
    "$env:ProgramFiles\Inno Setup *\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup *\ISCC.exe"
) | Where-Object { $_ } |
    ForEach-Object { Resolve-Path $_ -ErrorAction SilentlyContinue } |
    Select-Object -First 1 -ExpandProperty Path

if (-not $iscc) {
    Write-Warning "Inno Setup not found. Install it (winget install JRSoftware.InnoSetup) or pass -SkipInstaller."
    return
}

Write-Host "Building installer..." -ForegroundColor Cyan
& $iscc "/DAppVersion=$version" "/DPublishDir=$publishDir" (Join-Path $root "installer\VertiRPC.iss")
if ($LASTEXITCODE -ne 0) { throw "Installer build failed" }

Write-Host "Installer: $(Join-Path $root "installer\Output\VertiRPC-$version-Setup.exe")" -ForegroundColor Green
