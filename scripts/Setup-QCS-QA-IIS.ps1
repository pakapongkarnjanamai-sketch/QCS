<#
.SYNOPSIS
    Sets up QCS IIS applications and app pools on the QA web server (AP-NTC2138-QAWB).
    Run this script on the IIS server itself as Administrator, or invoke it remotely:
        $cred = Get-Credential
        Invoke-Command -ComputerName AP-NTC2138-QAWB -Credential $cred -FilePath .\scripts\Setup-QCS-QA-IIS.ps1

.DESCRIPTION
    Creates the following IIS application structure under Default Web Site:
        /QCS              -> static redirect to /QCS/User  -> QCS-Web-Pool (No Managed Code)
        /QCS/Service      -> QCS.API (REST API)            -> QCS-Api-Pool
        /QCS/PDF          -> PDF.Service (Document API)    -> QCS-Pdf-Pool
        /QCS/Admin        -> QCS.React.Admin (Static SPA)  -> QCS-Admin-Pool
        /QCS/User         -> QCS.React.User (Static SPA)   -> QCS-User-Pool

    /QCS hosted the MVC portal until PLAN-051 Phase 6. It remains an IIS application only because
    the four applications below are nested under it; it now serves one web.config and no code.
    QCS-User-Pool belongs to QCS.React.User and must NOT be removed with the MVC portal — the pool
    that went with MVC is QCS-Web-Pool, and it is kept for the redirect.

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
    # /QCS was the MVC portal. PLAN-051 Phase 6 removed it; this is now a static redirect to
    # /QCS/User so existing bookmarks keep working. It stays an IIS application only because the
    # sub-applications below live under it — the pool runs No Managed Code and serves one
    # web.config, no .NET application. Anonymous is required: a redirect the browser cannot reach
    # without authenticating first is not a redirect a bookmark survives.
    @{
        Name         = 'QCS'
        PhysicalPath = "$PhysicalRoot\QCS"
        PoolName     = 'QCS-Web-Pool'
        WindowsAuth  = $false
        AnonAuth     = $true
        RedirectTo   = '/QCS/User/'
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

<#
    Writes the web.config that turns /QCS into a one-hop redirect to the React portal.

    Two details matter and neither is optional:

    * inheritInChildApplications="false" — without it, /QCS/Service, /QCS/User, /QCS/Admin and
      /QCS/PDF inherit this httpRedirect and every one of them starts redirecting to /QCS/User/.
      That breaks the API and puts the SPA in a loop, and it looks like a routing bug rather than
      a config inheritance bug.
    * exactDestination="true" with childOnly="false" — the appended path must not be carried over.
      An old bookmark to /QCS/Request/Detail/5 has no equivalent path under the SPA, so it goes to
      the portal root rather than to a React route that does not exist.

    302, not 301: a permanent redirect is cached by browsers indefinitely and would outlive any
    decision to change this.
#>
function Write-RedirectStub {
    param(
        [string]$PhysicalPath,
        [string]$Destination
    )

    if (-not (Test-Path $PhysicalPath)) {
        New-Item -ItemType Directory -Path $PhysicalPath -Force | Out-Null
    }

    $configPath = Join-Path $PhysicalPath 'web.config'
    $content = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <httpRedirect enabled="true" destination="$Destination" exactDestination="true" childOnly="false" httpResponseStatus="Found" />
    </system.webServer>
  </location>
</configuration>
"@
    Set-Content -Path $configPath -Value $content -Encoding UTF8
    Write-Host "    Redirect: /QCS -> $Destination" -ForegroundColor DarkCyan
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
    if ($app.ContainsKey('RedirectTo')) {
        Write-RedirectStub -PhysicalPath $app.PhysicalPath -Destination $app.RedirectTo
    }
}

# The old MVC application left its binaries and views under $PhysicalRoot\QCS. They are dead files
# that would still be served if any path happened to match, so name what is expected to remain.
Write-Host "`n=== Leftover MVC content under $PhysicalRoot\QCS ===" -ForegroundColor Yellow
$expected = @('web.config', 'Service', 'PDF', 'Admin', 'User', 'logs', '_backup')
$leftovers = @(Get-ChildItem -LiteralPath "$PhysicalRoot\QCS" -Force -ErrorAction SilentlyContinue |
    Where-Object { $expected -notcontains $_.Name })
if ($leftovers.Count -eq 0) {
    Write-Host "[*] None." -ForegroundColor Green
} else {
    # Reported, never deleted here. This script provisions; removing deployed content is a
    # decision for whoever is doing the cutover, with a backup taken first.
    Write-Host "[!] Remove these by hand once the redirect is verified:" -ForegroundColor Yellow
    $leftovers | ForEach-Object { Write-Host "    $($_.Name)" -ForegroundColor Yellow }
}

# Restart all QCS app pools
Write-Host "`n=== Restarting app pools ===" -ForegroundColor Yellow
foreach ($app in $Apps) {
    Restart-WebAppPool -Name $app.PoolName -ErrorAction SilentlyContinue
    Write-Host "[*] Restarted: $($app.PoolName)" -ForegroundColor Green
}

Write-Host "`n=== QCS QA IIS Setup Complete ===" -ForegroundColor Green
Write-Host "Next: run Deploy-QA.ps1 to publish and copy application files." -ForegroundColor Green
