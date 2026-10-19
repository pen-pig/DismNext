# build.ps1 -- Dism-Plus-Next one-click build/publish script.
# Auto-detects the .NET 10 SDK and publishes the GUI + CLI into publish/.
# Usage: double-click build.cmd, or run  .\build.ps1  in PowerShell.
param(
    [string]$Config = "Release",
    [string]$Rid = "",
    [switch]$SelfContained
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# 1) Locate dotnet
# ---------------------------------------------------------------------------
$candidates = @()
$candidates += "$env:USERPROFILE\.dotnet-sdk\dotnet.exe"
$candidates += "$env:USERPROFILE\.dotnet\dotnet.exe"
$pc = Get-Command dotnet -ErrorAction SilentlyContinue
if ($pc) { $candidates += $pc.Source }
$candidates += "C:\Program Files\dotnet\dotnet.exe"
$candidates += "C:\Program Files (x86)\dotnet\dotnet.exe"

# Pick the first candidate that actually has an SDK (not just a runtime).
$dotnet = $null
foreach ($c in $candidates) {
    if ($c -and (Test-Path $c)) {
        try {
            $sdks = & $c --list-sdks 2>$null
            if ($sdks -and $sdks.Count -gt 0) { $dotnet = $c; break }
        } catch { }
    }
}
if (-not $dotnet) {
    Write-Error "Cannot find a .NET SDK. Install .NET 10 SDK from https://dotnet.microsoft.com/download"
    exit 1
}

Write-Host "==> Using dotnet: $dotnet"
& $dotnet --version

# ---------------------------------------------------------------------------
# 2) Go to the script directory and publish
# ---------------------------------------------------------------------------
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$outGui = Join-Path $root "publish\DismPlusNext"
$outCli = Join-Path $root "publish\DismPlusNext.Cli"

$scArgs = if ($SelfContained) { @("--self-contained") } else { @("--self-contained", "false") }
$ridArgs = if ($Rid) { @("-r", $Rid) } else { @() }

Write-Host "`n==> Publishing GUI (WPF, $Config, rid=$(if($Rid){$Rid}else{'portable'}))"
& $dotnet publish DismPlusNext/DismPlusNext.csproj -c $Config @ridArgs @scArgs -o $outGui
if ($LASTEXITCODE -ne 0) { Write-Error "GUI publish failed"; exit 1 }

Write-Host "`n==> Publishing CLI ($Config, rid=$(if($Rid){$Rid}else{'portable'}))"
& $dotnet publish DismPlusNext.Cli/DismPlusNext.Cli.csproj -c $Config @ridArgs @scArgs -o $outCli
if ($LASTEXITCODE -ne 0) { Write-Error "CLI publish failed"; exit 1 }

# ---------------------------------------------------------------------------
# 3) Done
# ---------------------------------------------------------------------------
Write-Host "`n================ Done ================"
Write-Host "GUI exe: $outGui\DismPlusNext.exe"
Write-Host "CLI exe: $outCli\DismPlusNext.Cli.exe"
Write-Host "Note: run as Administrator for cleanup/optimize; backup/restore needs system DISM."
