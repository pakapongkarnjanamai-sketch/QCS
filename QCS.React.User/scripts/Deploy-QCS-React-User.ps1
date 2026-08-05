[CmdletBinding()]
param(
    [string]$TargetPath = 'C:\inetpub\wwwroot\QCS\User',
    [string]$PublicBasePath = '/QCS/User',
    [string]$ApiBaseUrl = '/QCS/Service',
    [string]$HubUrl = '/QCS/Service/notificationHub',
    [string]$LegacyPortalBaseUrl = '/QCS',
    [string]$PublicSiteOrigin,
    [switch]$InstallDependencies,
    [switch]$SkipCopy,
    [switch]$SkipSmokeTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$DistPath = Join-Path $ProjectRoot 'dist'

# Every smoke check is AUTHENTICATED (--negotiate) and demands an exact 200.
#
# Two traps this closes. An anonymous request is challenged by Windows auth before routing, so it
# returns 401 for any URL whether the route exists or not - a check that can never fail and
# therefore proves nothing; that cost PLAN-036 a review. And accepting any 2xx-3xx would let a
# 302 pass, which for an SPA deep link means the fallback is misconfigured, not that it works.
function Invoke-CheckedRequest {
    param([string]$Url, [bool]$ExpectHtml, [string]$Label)
    $temporaryFile = [System.IO.Path]::GetTempFileName()
    try {
        $status = [int](& curl.exe -k -s -o $temporaryFile -w '%{http_code}' --negotiate -u ':' $Url)
        $global:LASTEXITCODE = 0
        if ($status -ne 200) { throw "${Label} expected 200 but got ${status}: $Url" }
        if ($ExpectHtml -and (Get-Content $temporaryFile -Raw) -notmatch '<!doctype html|<html') { throw "$Label did not return HTML: $Url" }
        Write-Host ("  {0,-28} 200  {1}" -f $Label, $Url) -ForegroundColor Green
    }
    finally { Remove-Item $temporaryFile -Force -ErrorAction SilentlyContinue }
}

Push-Location $ProjectRoot
try {
    if ($InstallDependencies) { npm ci; if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' } }
    npm run lint; if ($LASTEXITCODE -ne 0) { throw 'Lint failed.' }
    $env:VITE_QCS_USER_APP_BASE_PATH = $PublicBasePath
    $env:VITE_QCS_API_BASE_URL = $ApiBaseUrl
    $env:VITE_QCS_HUB_URL = $HubUrl
    $env:VITE_QCS_LEGACY_PORTAL_BASE_URL = $LegacyPortalBaseUrl
    # Artifact freshness: clear dist first, so a build that silently fails to emit cannot leave a
    # previous run's output to be shipped as if it were this one's.
    if (Test-Path $DistPath) { Remove-Item $DistPath -Recurse -Force }

    npm run build; if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    if (-not (Test-Path $DistPath)) { throw "dist folder was not created: $DistPath" }

    $indexPath = Join-Path $DistPath 'index.html'
    if (-not (Test-Path $indexPath)) { throw "dist has no index.html: $DistPath" }
    if (((Get-Item $indexPath).LastWriteTimeUtc) -lt (Get-Date).ToUniversalTime().AddMinutes(-30)) {
        throw "dist/index.html is older than this run - the build did not produce a fresh artifact."
    }
    $webConfig = Join-Path $DistPath 'web.config'
    (Get-Content $webConfig -Raw).Replace('/QCS/User/index.html', ($PublicBasePath.TrimEnd('/') + '/index.html')) | Set-Content $webConfig -NoNewline
    if (-not $SkipCopy) {
        New-Item -ItemType Directory -Path $TargetPath -Force | Out-Null
        & robocopy $DistPath $TargetPath /MIR /R:2 /W:1
        # robocopy signals SUCCESS with 1-7 (files copied, extras removed, and so on). Left alone,
        # that leaks out as this script's exit code and a CI gate reads a clean deploy as a
        # failure - the defect QRS already fixed in its own scripts under PLAN-025.
        if ($LASTEXITCODE -gt 7) { throw "robocopy failed with exit code $LASTEXITCODE" }
        $global:LASTEXITCODE = 0

        & icacls $TargetPath /grant 'IIS_IUSRS:(OI)(CI)(RX)' /T /C | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "icacls failed with exit code $LASTEXITCODE" }
        $global:LASTEXITCODE = 0
    }
    if ($PublicSiteOrigin -and -not $SkipSmokeTest) {
        $origin = $PublicSiteOrigin.TrimEnd('/')
        $base = $PublicBasePath.TrimEnd('/')
        Write-Host 'Smoke checks (authenticated, each must return exactly 200):' -ForegroundColor Cyan

        # Every SPA route the sidebar or a bookmark can reach. Each must serve the index fallback.
        Invoke-CheckedRequest "$origin$base/" $true 'SPA root'
        Invoke-CheckedRequest "$origin$base/requests" $true 'Requests deep link'
        Invoke-CheckedRequest "$origin$base/requests/1" $true 'Request deep link'
        Invoke-CheckedRequest "$origin$base/inbox" $true 'Approvals deep link'
        Invoke-CheckedRequest "$origin$base/quotations" $true 'Quotations deep link'

        $apiRoot = if ($ApiBaseUrl -match '^https?://') { $ApiBaseUrl.TrimEnd('/') } else { "$origin$($ApiBaseUrl.TrimEnd('/'))" }
        Invoke-CheckedRequest "$apiRoot/api/Session/Me" $false 'Session API'

        # The API the SPA cannot function without. Checked explicitly because the SPA shipping in
        # front of a Portal API that was never deployed is a mistake this project has already made.
        Invoke-CheckedRequest "$apiRoot/api/Portal/Requests?view=MyRequests&page=1&pageSize=1" $false 'Portal API'

        # The legacy portal must still answer while it remains the rollback path.
        Invoke-CheckedRequest "$origin$($LegacyPortalBaseUrl.TrimEnd('/'))/" $true 'Legacy portal root'
    }

    Write-Host 'QCS.React.User deployment completed.' -ForegroundColor Green
    exit 0
}
catch {
    Write-Host "QCS.React.User deployment FAILED: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
finally { Pop-Location }