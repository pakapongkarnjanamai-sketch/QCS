[CmdletBinding()]
param(
    [string]$TargetPath = '\\10.10.154.21\wwwroot\QCS\Admin',
    [string]$PublicBasePath = '/QCS/admin',
    [string]$ApiBaseUrl = '/QCS/Service',
    [string]$HubUrl = '/QCS/Service/hubs/qcs',
    [string]$PortalBaseUrl = '/QCS',
    [string]$PublicSiteOrigin = 'https://ap-ntc2137-prwb',
    [switch]$InstallDependencies,
    [switch]$SkipCopy,
    [switch]$SkipSmokeTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$DistPath = Join-Path $ProjectRoot 'dist'

function Write-Step {
    param([string]$Message)

    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Normalize-PathPrefix {
    param([string]$Value)

    $trimmed = $Value.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed -eq '/') {
        return '/'
    }

    return '/' + ($trimmed -replace '^/+', '' -replace '/+$', '')
}

function Join-PublicUrl {
    param(
        [string]$Origin,
        [string]$Path
    )

    $normalizedOrigin = $Origin.TrimEnd('/')
    $normalizedPath = Normalize-PathPrefix $Path
    if ($normalizedPath -eq '/') {
        return "$normalizedOrigin/"
    }

    return "$normalizedOrigin$normalizedPath"
}

function Update-SpaWebConfig {
    param(
        [string]$WebConfigPath,
        [string]$BasePath
    )

    if (-not (Test-Path $WebConfigPath)) {
        return
    }

    $spaIndexPath = (Normalize-PathPrefix $BasePath).TrimEnd('/') + '/index.html'
    $content = Get-Content -Path $WebConfigPath -Raw
    $updated = $content -replace 'path="index\.html"', ('path="' + $spaIndexPath + '"')

    if ($updated -ne $content) {
        Set-Content -Path $WebConfigPath -Value $updated -Encoding UTF8
        Write-Host ("Stamped SPA fallback path in web.config: {0}" -f $spaIndexPath) -ForegroundColor DarkCyan
    }
}

function Invoke-CheckedRequest {
    param(
        [string]$Url,
        [bool]$ExpectHtml = $false,
        [string]$Label,
        [int]$MaxAttempts = 3
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseDefaultCredentials -SkipCertificateCheck -SkipHttpErrorCheck
            if ($response.StatusCode -ge 500 -or $response.StatusCode -eq 404) {
                throw "$Label failed with status $($response.StatusCode): $Url"
            }

            if ($ExpectHtml -and $response.Content -notmatch '<!doctype html|<html') {
                throw "$Label did not return HTML: $Url"
            }

            Write-Host ("{0}: {1} ({2})" -f $Label, $response.StatusCode, $Url) -ForegroundColor Green
            return
        }
        catch {
            if ($attempt -eq $MaxAttempts) {
                throw
            }
        }
    }
}

Push-Location $ProjectRoot
try {
    Write-Step 'Deployment checklist'
    Write-Host ("ProjectRoot     : {0}" -f $ProjectRoot)
    Write-Host ("TargetPath      : {0}" -f $TargetPath)
    Write-Host ("PublicBasePath  : {0}" -f $PublicBasePath)
    Write-Host ("ApiBaseUrl      : {0}" -f $ApiBaseUrl)
    Write-Host ("HubUrl          : {0}" -f $HubUrl)
    Write-Host ("PortalBaseUrl   : {0}" -f $PortalBaseUrl)
    if ($PublicSiteOrigin) {
        Write-Host ("PublicSiteOrigin: {0}" -f $PublicSiteOrigin)
    }

    if ($InstallDependencies) {
        Write-Step 'Installing frontend dependencies'
        npm ci
        if ($LASTEXITCODE -ne 0) {
            throw 'npm ci failed.'
        }
    }

    Write-Step 'Running pre-deploy validation'
    npm run lint
    if ($LASTEXITCODE -ne 0) {
        throw 'Lint failed.'
    }

    $originalBase = $env:VITE_QCS_ADMIN_APP_BASE_PATH
    $originalApi = $env:VITE_QCS_API_BASE_URL
    $originalHub = $env:VITE_QCS_HUB_URL
    $originalPortal = $env:VITE_QCS_PORTAL_BASE_URL

    try {
        $env:VITE_QCS_ADMIN_APP_BASE_PATH = $PublicBasePath
        $env:VITE_QCS_API_BASE_URL = $ApiBaseUrl
        $env:VITE_QCS_HUB_URL = $HubUrl
        $env:VITE_QCS_PORTAL_BASE_URL = $PortalBaseUrl

        Write-Step 'Building production artifact with explicit environment values'
        npm run build
        if ($LASTEXITCODE -ne 0) {
            throw 'Production build failed.'
        }
    }
    finally {
        $env:VITE_QCS_ADMIN_APP_BASE_PATH = $originalBase
        $env:VITE_QCS_API_BASE_URL = $originalApi
        $env:VITE_QCS_HUB_URL = $originalHub
        $env:VITE_QCS_PORTAL_BASE_URL = $originalPortal
    }

    if (-not (Test-Path $DistPath)) {
        throw "dist folder was not created: $DistPath"
    }

    Update-SpaWebConfig -WebConfigPath (Join-Path $DistPath 'web.config') -BasePath $PublicBasePath

    if (-not $SkipCopy) {
        Write-Step 'Copying dist to IIS target via robocopy'
        New-Item -ItemType Directory -Path $TargetPath -Force | Out-Null
        & robocopy $DistPath $TargetPath /MIR /R:2 /W:1
        if ($LASTEXITCODE -gt 7) {
            throw "robocopy failed with exit code $LASTEXITCODE"
        }
    }

    if ($PublicSiteOrigin -and -not $SkipSmokeTest) {
        Write-Step 'Running smoke tests against deployed URLs'

        $rootUrl = Join-PublicUrl -Origin $PublicSiteOrigin -Path $PublicBasePath
        if (-not $rootUrl.EndsWith('/')) {
            $rootUrl = "$rootUrl/"
        }

        $requestsPath = (Normalize-PathPrefix $PublicBasePath).TrimEnd('/') + '/requests'
        $quotationsPath = (Normalize-PathPrefix $PublicBasePath).TrimEnd('/') + '/quotations'
        $requestsUrl = Join-PublicUrl -Origin $PublicSiteOrigin -Path $requestsPath
        $quotationsUrl = Join-PublicUrl -Origin $PublicSiteOrigin -Path $quotationsPath

        $apiRootUrl = if ($ApiBaseUrl -match '^https?://') {
            $ApiBaseUrl.TrimEnd('/')
        }
        else {
            (Join-PublicUrl -Origin $PublicSiteOrigin -Path $ApiBaseUrl).TrimEnd('/')
        }

        Invoke-CheckedRequest -Url $rootUrl -ExpectHtml $true -Label 'SPA root'
        Invoke-CheckedRequest -Url $requestsUrl -ExpectHtml $true -Label 'SPA requests deep link'
        Invoke-CheckedRequest -Url $quotationsUrl -ExpectHtml $true -Label 'SPA quotations deep link'
        Invoke-CheckedRequest -Url "$apiRootUrl/api/Dashboard/Summary" -Label 'Dashboard API'
    }
    elseif (-not $SkipSmokeTest) {
        Write-Step 'Smoke tests skipped because PublicSiteOrigin was not provided'
    }

    Write-Step 'Deploy completed successfully'
}
finally {
    Pop-Location
}