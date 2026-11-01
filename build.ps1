# ShakeShake build script
# Uses the C# 5 compiler that ships with Windows (.NET Framework 4.8) - no SDK needed.
param(
    [switch]$Tests
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    throw "Cannot find the .NET Framework compiler: $csc"
}

New-Item -ItemType Directory -Force (Join-Path $root 'dist') | Out-Null
$out = Join-Path $root 'dist\ShakeShake.exe'
$icon = Join-Path $root 'assets\app.ico'

$sources = Get-ChildItem (Join-Path $root 'src\*.cs') | ForEach-Object { $_.FullName }
$common = @(
    '/nologo', '/codepage:65001', '/optimize+',
    '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll'
)

$appArgs = @('/target:winexe', '/platform:x64', ('/out:' + $out))
if (Test-Path $icon) { $appArgs += ('/win32icon:' + $icon) }

& $csc @($appArgs + $common + $sources)
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$exe = Get-Item $out
"built: {0}  ({1:N0} bytes)" -f $exe.FullName, $exe.Length

if ($Tests) {
    $testOut = Join-Path $root 'tests\DetectorTests.exe'
    & $csc @('/target:exe', ('/out:' + $testOut), '/r:System.dll', '/r:System.Core.dll',
              '/nologo', '/codepage:65001',
              (Join-Path $root 'src\ShakeDetector.cs'),
              (Join-Path $root 'tests\DetectorTests.cs'))
    if ($LASTEXITCODE -ne 0) { throw "test build failed" }
    & $testOut
    if ($LASTEXITCODE -ne 0) { throw "tests failed" }

    $uiOut = Join-Path $root 'tests\UiLayoutTests.exe'
    $uiArgs = @('/target:exe', ('/out:' + $uiOut), '/main:ShakeShake.Tests.UiLayoutTests',
                '/nologo', '/codepage:65001',
                '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll')
    & $csc @($uiArgs + $sources + (Join-Path $root 'tests\UiLayoutTests.cs'))
    if ($LASTEXITCODE -ne 0) { throw "ui test build failed" }
    & $uiOut
    if ($LASTEXITCODE -ne 0) { throw "ui tests failed" }
}
