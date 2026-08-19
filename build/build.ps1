[CmdletBinding()]
param(
    [string]$OutDir
)
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) { $OutDir = Join-Path $repo 'dist' }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe bulunamadi: $csc" }

# /noconfig ile otomatik csc.rsp referanslarini kapatiyoruz; hepsini framework dizininden veriyoruz (cift referans olmaz).
$fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$refNames = @(
    'System.dll',
    'System.Core.dll',
    'System.Xml.dll',
    'System.Windows.Forms.dll',
    'System.Drawing.dll',
    'System.Web.Extensions.dll',
    'System.Net.Http.dll'
)
$refs = $refNames | ForEach-Object {
    $p = Join-Path $fw $_
    if (-not (Test-Path $p)) { throw "Referans bulunamadi: $p" }
    "/r:$p"
}
$refs = @('/noconfig') + $refs

# ---- LoginGuard.exe (tray + capture) ----
$appSrc = Join-Path $repo 'src\LoginGuard'
$appFiles = Get-ChildItem "$appSrc\*.cs" | ForEach-Object { $_.FullName }
$appManifest = Join-Path $appSrc 'app.manifest'
$appOut = Join-Path $OutDir 'LoginGuard.exe'

Write-Host "LoginGuard.exe derleniyor..."
$argsApp = @('/nologo','/target:winexe',"/out:$appOut","/win32manifest:$appManifest") + $refs + $appFiles
& $csc @argsApp
if ($LASTEXITCODE -ne 0) { throw "LoginGuard.exe derleme HATASI ($LASTEXITCODE)" }
Write-Host "  -> $appOut"

# ---- LoginGuardSetup.exe (yukseltme isteyen kurulum) ----
$setupSrc = Join-Path $repo 'src\LoginGuardSetup'
if (Test-Path $setupSrc) {
    $setupFiles = Get-ChildItem "$setupSrc\*.cs" -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName }
    $setupManifest = Join-Path $setupSrc 'setup.manifest'
    if ($setupFiles) {
        $setupOut = Join-Path $OutDir 'LoginGuardSetup.exe'
        Write-Host "LoginGuardSetup.exe derleniyor..."
        $argsSetup = @('/nologo','/target:exe',"/out:$setupOut","/win32manifest:$setupManifest") + $refs + $setupFiles
        & $csc @argsSetup
        if ($LASTEXITCODE -ne 0) { throw "LoginGuardSetup.exe derleme HATASI ($LASTEXITCODE)" }
        Write-Host "  -> $setupOut"
    }
}

Write-Host "Derleme tamam."
