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

function Invoke-CheckedRequest {
    param([string]$Url, [bool]$ExpectHtml, [string]$Label)
    $temporaryFile = [System.IO.Path]::GetTempFileName()
    try {
        $status = [int](& curl.exe -k -s -o $temporaryFile -w '%{http_code}' --negotiate -u ':' $Url)
        if ($status -lt 200 -or $status -ge 400) { throw "${Label} failed with status ${status}: $Url" }
        if ($ExpectHtml -and (Get-Content $temporaryFile -Raw) -notmatch '<!doctype html|<html') { throw "$Label did not return HTML: $Url" }
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
    npm run build; if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    if (-not (Test-Path $DistPath)) { throw "dist folder was not created: $DistPath" }
    $webConfig = Join-Path $DistPath 'web.config'
    (Get-Content $webConfig -Raw).Replace('/QCS/User/index.html', ($PublicBasePath.TrimEnd('/') + '/index.html')) | Set-Content $webConfig -NoNewline
    if (-not $SkipCopy) {
        New-Item -ItemType Directory -Path $TargetPath -Force | Out-Null
        & robocopy $DistPath $TargetPath /MIR /R:2 /W:1
        if ($LASTEXITCODE -gt 7) { throw "robocopy failed with exit code $LASTEXITCODE" }
        & icacls $TargetPath /grant 'IIS_IUSRS:(OI)(CI)(RX)' /T /C | Out-Null
    }
    if ($PublicSiteOrigin -and -not $SkipSmokeTest) {
        $origin = $PublicSiteOrigin.TrimEnd('/')
        $base = $PublicBasePath.TrimEnd('/')
        Invoke-CheckedRequest "$origin$base/" $true 'SPA root'
        Invoke-CheckedRequest "$origin$base/requests" $true 'Requests deep link'
        Invoke-CheckedRequest "$origin$base/requests/1" $true 'Request deep link'
        $apiRoot = if ($ApiBaseUrl -match '^https?://') { $ApiBaseUrl.TrimEnd('/') } else { "$origin$($ApiBaseUrl.TrimEnd('/'))" }
        Invoke-CheckedRequest "$apiRoot/api/Session/Me" $false 'Session API'
    }
}
finally { Pop-Location }