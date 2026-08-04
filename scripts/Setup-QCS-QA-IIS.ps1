<#
.SYNOPSIS
    Sets up QCS IIS applications and app pools on the QA web server (AP-NTC2138-QAWB).
    Run this script on the IIS server itself as Administrator, or invoke it remotely:
        $cred = Get-Credential
        Invoke-Command -ComputerName AP-NTC2138-QAWB -Credential $cred -FilePath .\scripts\Setup-QCS-QA-IIS.ps1

.DESCRIPTION
    Creates the following IIS application structure under Default Web Site:
        /QCS              -> QCS.Web.User (MVC)           -> QCS-Web-Pool
        /QCS/Service      -> QCS.API (REST API)            -> QCS-Api-Pool
        /QCS/PDF          -> PDF.Service (Document API)    -> QCS-Pdf-Pool
        /QCS/Admin        -> QCS.React.Admin (Static SPA)  -> QCS-Admin-Pool

    All app pools: No Managed Code, Integrated pipeline.
    Auth: Windows Auth ON / Anonymous OFF for .NET apps; Anonymous ON / Windows OFF for React SPA.
    Auth settings written to applicationHost.config via appcmd /commit:apphost (avoids locked-section errors).
#>

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Import-Module WebAdministration

$Site = 'Default Web Site'
$PhysicalRoot = 'C:\inetpub\wwwroot'
$appcmd = Join-Path $env:SystemRoot 'System32\inetsrv\appcmd.exe'

# ── App definitions ──────────────────────────────────────────────────────
$Apps = @(
    @{
        Name         = 'QCS'
        PhysicalPath = "$PhysicalRoot\QCS"
        PoolName     = 'QCS-Web-Pool'
        WindowsAuth  = $true
        AnonAuth     = $false
    },
    @{
        Name         = 'QCS/Service'
        PhysicalPath = "$PhysicalRoot\QCS\Service"
        PoolName     = 'QCS-Api-Pool'
        WindowsAuth  = $true
        AnonAuth     = $true
    },
    @{
        Name         = 'QCS/PDF'
        PhysicalPath = "$PhysicalRoot\QCS\PDF"
        PoolName     = 'QCS-Pdf-Pool'
        WindowsAuth  = $false
        AnonAuth     = $true
    },
    @{
        Name         = 'QCS/Admin'
        PhysicalPath = "$PhysicalRoot\QCS\Admin"
        PoolName     = 'QCS-Admin-Pool'
        WindowsAuth  = $false
        AnonAuth     = $true
    },
    @{
        Name         = 'QCS/User'
        PhysicalPath = "$PhysicalRoot\QCS\User"
        PoolName     = 'QCS-User-Pool'
        WindowsAuth  = $false
        AnonAuth     = $true
    }
)

function Ensure-AppPool {
    param([string]$PoolName)

    if (-not (Test-Path "IIS:\AppPools\$PoolName")) {
        Write-Host "[+] Creating app pool: $PoolName" -ForegroundColor Green
        New-WebAppPool -Name $PoolName | Out-Null
    }
    else {
        Write-Host "[=] App pool exists: $PoolName" -ForegroundColor DarkGray
    }
    Set-ItemProperty "IIS:\AppPools\$PoolName" -Name managedRuntimeVersion -Value ''
    Set-ItemProperty "IIS:\AppPools\$PoolName" -Name processModel.identityType -Value 'ApplicationPoolIdentity'
}

function Ensure-WebApplication {
    param(
        [string]$SiteName,
        [string]$AppName,
        [string]$PhysicalPath,
        [string]$PoolName
    )

    # Ensure physical directory exists
    if (-not (Test-Path $PhysicalPath)) {
        New-Item -ItemType Directory -Path $PhysicalPath -Force | Out-Null
        Write-Host "[+] Created directory: $PhysicalPath" -ForegroundColor Green
    }

    $existingApp = Get-WebApplication -Site $SiteName -Name $AppName -ErrorAction SilentlyContinue
    if (-not $existingApp) {
        Write-Host "[+] Creating IIS application: /$AppName" -ForegroundColor Green
        New-WebApplication -Site $SiteName -Name $AppName -PhysicalPath $PhysicalPath -ApplicationPool $PoolName | Out-Null
    }
    else {
        Write-Host "[=] IIS application exists: /$AppName — updating pool and path" -ForegroundColor DarkGray
        $webPath = "IIS:\Sites\$SiteName\$AppName"
        Set-ItemProperty $webPath -Name physicalPath -Value $PhysicalPath -ErrorAction SilentlyContinue
        Set-ItemProperty $webPath -Name applicationPool -Value $PoolName -ErrorAction SilentlyContinue
    }
}

function Set-AppAuth {
    param(
        [string]$AppPath,
        [bool]$WindowsAuth,
        [bool]$AnonAuth
    )

    $winAuthValue = if ($WindowsAuth) { 'true' } else { 'false' }
    $anonAuthValue = if ($AnonAuth) { 'true' } else { 'false' }

    Write-Host "    Auth: Windows=$winAuthValue, Anonymous=$anonAuthValue" -ForegroundColor DarkCyan
    & $appcmd set config "$AppPath" "-section:system.webServer/security/authentication/windowsAuthentication" "-enabled:$winAuthValue" /commit:apphost 2>&1 | Out-Null
    & $appcmd set config "$AppPath" "-section:system.webServer/security/authentication/anonymousAuthentication" "-enabled:$anonAuthValue" /commit:apphost 2>&1 | Out-Null
}

function Grant-AppPoolAcl {
    param(
        [string]$PhysicalPath,
        [string]$PoolName,
        [bool]$AnonAuth
    )

    Write-Host "    ACL: IIS AppPool\$PoolName -> RX" -ForegroundColor DarkCyan
    & icacls $PhysicalPath /grant "IIS AppPool\${PoolName}:(OI)(CI)RX" /T /C /Q 2>&1 | Out-Null

    if ($AnonAuth) {
        Write-Host "    ACL: IUSR + IIS_IUSRS -> RX (anonymous)" -ForegroundColor DarkCyan
        & icacls $PhysicalPath /grant "IUSR:(OI)(CI)RX" /T /C /Q 2>&1 | Out-Null
        & icacls $PhysicalPath /grant "IIS_IUSRS:(OI)(CI)RX" /T /C /Q 2>&1 | Out-Null
    }
}

# ── Ensure logs directories exist and are writable ──────────────────────
$logsDirs = @("$PhysicalRoot\QCS\logs", "$PhysicalRoot\QCS\Service\logs")
foreach ($logsDir in $logsDirs) {
    if (-not (Test-Path $logsDir)) {
        New-Item -ItemType Directory -Path $logsDir -Force | Out-Null
    }
    & icacls $logsDir /grant "IIS_IUSRS:(OI)(CI)M" /T /C /Q 2>&1 | Out-Null
}

# ── Main ────────────────────────────────────────────────────────────────
Write-Host "`n=== QCS QA IIS Setup ===" -ForegroundColor Yellow
Write-Host "Site         : $Site" -ForegroundColor Yellow
Write-Host "Physical Root: $PhysicalRoot" -ForegroundColor Yellow
Write-Host ""

foreach ($app in $Apps) {
    Write-Host "`n--- /$($app.Name) ---" -ForegroundColor Cyan
    Ensure-AppPool -PoolName $app.PoolName
    Ensure-WebApplication -SiteName $Site -AppName $app.Name -PhysicalPath $app.PhysicalPath -PoolName $app.PoolName
    Set-AppAuth -AppPath "$Site/$($app.Name)" -WindowsAuth $app.WindowsAuth -AnonAuth $app.AnonAuth
    Grant-AppPoolAcl -PhysicalPath $app.PhysicalPath -PoolName $app.PoolName -AnonAuth $app.AnonAuth
}

# Restart all QCS app pools
Write-Host "`n=== Restarting app pools ===" -ForegroundColor Yellow
foreach ($app in $Apps) {
    Restart-WebAppPool -Name $app.PoolName -ErrorAction SilentlyContinue
    Write-Host "[*] Restarted: $($app.PoolName)" -ForegroundColor Green
}

Write-Host "`n=== QCS QA IIS Setup Complete ===" -ForegroundColor Green
Write-Host "Next: run Deploy-QA.ps1 to publish and copy application files." -ForegroundColor Green
