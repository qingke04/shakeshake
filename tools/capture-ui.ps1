# Captures UI screenshots for the README / release notes.
# Usage:  powershell -ExecutionPolicy Bypass -File .\tools\capture-ui.ps1
#
# It temporarily replaces %APPDATA%\ShakeShake\settings.ini to get deterministic
# screenshots and restores the user's file afterwards.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$exe = Join-Path $root "src\ShakeShake.App\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\ShakeShake.exe"
if (-not (Test-Path $exe)) { throw "build the app first: $exe not found" }

$settingsDir = Join-Path $env:APPDATA "ShakeShake"
$settingsFile = Join-Path $settingsDir "settings.ini"
New-Item -ItemType Directory -Force $settingsDir | Out-Null
$backup = "$settingsFile.capture-backup"
if (Test-Path $settingsFile) { Copy-Item $settingsFile $backup -Force }

$shotsDir = Join-Path $root "screenshots"
New-Item -ItemType Directory -Force $shotsDir | Out-Null

Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class UiCapture {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lp);
    [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    public delegate bool EnumProc(IntPtr h, IntPtr lp);
}
'@

function Stop-App {
    Get-Process -Name "ShakeShake" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

function Write-Settings([string]$language) {
    $text = @"
# ShakeShake / 晃晃  settings -- edit with care / 可直接修改，但请保持格式
[general]
language=$language
enabled=1
zoom=3.0
autostart=1

[advanced]
sensitivity=5
restore_ms=100
hotkey=Ctrl+Alt+S

[exclude]
"@
    [System.IO.File]::WriteAllText($settingsFile, $text, (New-Object System.Text.UTF8Encoding($true)))
}

function Find-Window([int]$processId) {
    for ($i = 0; $i -lt 40; $i++) {
        $p = Get-Process -Id $processId -ErrorAction SilentlyContinue
        if (-not $p) { return [IntPtr]::Zero }
        if ($p.MainWindowHandle -ne 0) { return $p.MainWindowHandle }
        $script:hit = [IntPtr]::Zero
        $callback = [UiCapture+EnumProc]{
            param($h, $lp)
            $found = 0
            [UiCapture]::GetWindowThreadProcessId($h, [ref]$found) | Out-Null
            if ($found -eq $processId -and [UiCapture]::IsWindowVisible($h)) { $script:hit = $h; return $false }
            return $true
        }
        [UiCapture]::EnumWindows($callback, [IntPtr]::Zero) | Out-Null
        if ($script:hit -ne [IntPtr]::Zero) { return $script:hit }
        Start-Sleep -Milliseconds 300
    }
    return [IntPtr]::Zero
}

function Capture([string]$name, [hashtable]$envVars, [string]$language) {
    Stop-App
    Write-Settings $language
    Start-Sleep -Milliseconds 300

    foreach ($key in $envVars.Keys) { Set-Item -Path ("env:" + $key) -Value $envVars[$key] }
    $p = Start-Process -FilePath $exe -ArgumentList "--settings" -PassThru
    try {
        Start-Sleep -Seconds 2
        $hwnd = Find-Window $p.Id
        if ($hwnd -eq [IntPtr]::Zero) { throw "window not found for capture '$name'" }
        [UiCapture]::ShowWindow($hwnd, 9) | Out-Null
        [UiCapture]::SetForegroundWindow($hwnd) | Out-Null
        Start-Sleep -Milliseconds 1500

        $rect = New-Object UiCapture+RECT
        [UiCapture]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
        $w = $rect.Right - $rect.Left
        $h = $rect.Bottom - $rect.Top
        if ($w -le 0 -or $h -le 0) { throw "bad window rect for '$name'" }

        $bmp = New-Object System.Drawing.Bitmap $w, $h
        $graphics = [System.Drawing.Graphics]::FromImage($bmp)
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
        $out = Join-Path $shotsDir ($name + ".png")
        $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
        $graphics.Dispose()
        $bmp.Dispose()
        "captured $out  ($w x $h)"
    }
    finally {
        Stop-App
        foreach ($key in $envVars.Keys) { Remove-Item -Path ("env:" + $key) -ErrorAction SilentlyContinue }
    }
}

try {
    Capture "zh-main"     @{}                          "zh-CN"
    Capture "zh-advanced" @{ SHAKESHAKE_UI_ADVANCED = "1" } "zh-CN"
    Capture "zh-dark"     @{ SHAKESHAKE_UI_THEME = "dark" } "zh-CN"
    Capture "en-main"     @{}                          "en-US"
    Capture "en-dark"     @{ SHAKESHAKE_UI_THEME = "dark" } "en-US"
    Capture "zh-menu"     @{ SHAKESHAKE_UI_MENU = "1" } "zh-CN"
}
finally {
    Stop-App
    if (Test-Path $backup) {
        Move-Item $backup $settingsFile -Force
    }
    "done; user settings restored"
}
