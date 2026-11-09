# ShakeShake build script
# Builds the WinUI 3 app, runs the unit tests, publishes a self-contained folder
# and (optionally) compiles the Inno Setup installer.
#
# Requirements:
#   - .NET 8 SDK (https://dotnet.microsoft.com/download/dotnet/8.0)
#   - Inno Setup 7 (only needed for -Installer)
#
# Examples:
#   powershell -ExecutionPolicy Bypass -File .\build.ps1                 # build only
#   powershell -ExecutionPolicy Bypass -File .\build.ps1 -Tests          # build + run tests
#   powershell -ExecutionPolicy Bypass -File .\build.ps1 -Tests -Installer
param(
    [switch]$Tests,
    [switch]$Installer,
    [string]$Configuration = "Release",
    [string]$Iscc = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

# ---------------------------------------------------------------- tools

$dotnet = Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) {
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) { $dotnet = $cmd.Source }
}
if (-not $dotnet) {
    throw "dotnet SDK not found. Install the .NET 8 SDK first (see README.md)."
}

$appProject = Join-Path $root "src\ShakeShake.App\ShakeShake.App.csproj"
$publishDir = Join-Path $root "dist\app"

$version = ""
$m = Select-String -Path $appProject -Pattern "<Version>([^<]+)</Version>"
if ($m) { $version = $m.Matches[0].Groups[1].Value }
if (-not $version) { throw "cannot read <Version> from $appProject" }

"dotnet  : $dotnet"
"version : $version"

# ---------------------------------------------------------------- build

& $dotnet build $appProject -c $Configuration -p:Platform=x64 -v m
if ($LASTEXITCODE -ne 0) { throw "build failed" }

if ($Tests) {
    & $dotnet run --project (Join-Path $root "tests\ShakeShake.Tests\ShakeShake.Tests.csproj") -c $Configuration -v q
    if ($LASTEXITCODE -ne 0) { throw "tests failed" }
}

# ---------------------------------------------------------------- publish

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
& $dotnet publish $appProject -c $Configuration -r win-x64 --self-contained true `
    -p:Platform=x64 -p:WindowsAppSDKSelfContained=true -o $publishDir -v m
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

# The WinUI XAML compiler writes *.xbf / <AssemblyName>.pri into the build output but
# the publish pipeline does not carry them along; without them the unpackaged app
# cannot load its XAML. Copy them next to the published exe.
$buildOut = Join-Path $root ("src\ShakeShake.App\bin\x64\" + $Configuration)
$pri = Get-ChildItem $buildOut -Recurse -File -Filter "ShakeShake.pri" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($pri) {
    Copy-Item $pri.FullName (Join-Path $publishDir "ShakeShake.pri") -Force
    Get-ChildItem $pri.Directory -File -Filter "*.xbf" | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $publishDir $_.Name) -Force
    }
} else {
    throw "ShakeShake.pri not found under $buildOut (did the XAML compiler run?)"
}

$exe = Join-Path $publishDir "ShakeShake.exe"
if (-not (Test-Path $exe)) { throw "published exe not found: $exe" }
"published: $exe"

# ---------------------------------------------------------------- installer

if ($Installer) {
    $isccPath = $Iscc
    if (-not $isccPath) {
        # Prefer an Inno Setup 7 install over whatever happens to be on PATH:
        # older 6.x compilers may lack the bundled ChineseSimplified.isl language file.
        $candidates = @(
            (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 7\ISCC.exe"),
            (Join-Path ${env:ProgramFiles} "Inno Setup 7\ISCC.exe"),
            (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 7\ISCC.exe"),
            (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
            (Join-Path ${env:ProgramFiles} "Inno Setup 6\ISCC.exe"),
            (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe")
        )
        foreach ($candidate in $candidates) {
            if (Test-Path $candidate) { $isccPath = $candidate; break }
        }
    }
    if (-not $isccPath) {
        $isccCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
        if ($isccCommand) { $isccPath = $isccCommand.Source }
    }
    if (-not $isccPath) {
        throw "ISCC.exe (Inno Setup compiler) not found. Install Inno Setup 7 or pass -Iscc <path>."
    }

    "iscc    : $isccPath"
    & $isccPath "/DAppVersion=$version" (Join-Path $root "installer\ShakeShake.iss")
    if ($LASTEXITCODE -ne 0) { throw "installer build failed" }

    $setup = Join-Path $root ("dist\ShakeShake-" + $version + "-Setup.exe")
    if (Test-Path $setup) { "setup   : $setup" }
}
