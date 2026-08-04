[CmdletBinding()]
param(
    [string]$ProjectPath = 'c:\Users\n4734\source\repos\QCS\PDF.Service\PDF.Service.csproj',
    [string]$TargetPath = '\\10.10.154.21\wwwroot\QCS\PDF',
    [string]$PublishPath = 'c:\Users\n4734\source\repos\QCS\artifacts\publish\PDF.Service',
    [string]$PublicServiceBaseUrl = 'http://ap-ntc2137-prwb/QCS/PDF',
    [switch]$SkipHealthCheck
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Step {
    param([string]$Message)
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Test-ServiceHealth {
    param(
        [string]$Url,
        [string]$Label,
        [int[]]$AcceptedStatusCodes = @(200),
        [int]$MaxAttempts = 3
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseDefaultCredentials -Method Head
            if ($AcceptedStatusCodes -contains [int]$response.StatusCode) {
                Write-Host ("{0}: {1} ({2})" -f $Label, $response.StatusCode, $Url) -ForegroundColor Green
                return $true
            }

            throw "$Label failed with status $($response.StatusCode): $Url"
        }
        catch {
            $statusCode = $null
            $resp = $_.Exception | Select-Object -ExpandProperty Response -ErrorAction SilentlyContinue
            if ($resp) {
                $statusCode = [int]($resp | Select-Object -ExpandProperty StatusCode -ErrorAction SilentlyContinue)
            }

            if ($statusCode -and ($AcceptedStatusCodes -contains $statusCode)) {
                Write-Host ("{0}: {1} ({2})" -f $Label, $statusCode, $Url) -ForegroundColor Green
                return $true
            }

            if ($attempt -eq $MaxAttempts) {
                Write-Host ("{0}: FAILED - {1}" -f $Label, $_.Exception.Message) -ForegroundColor Yellow
                return $false
            }
            Start-Sleep -Seconds 2
        }
    }
    return $false
}

function Backup-FileIfExists {
    param(
        [string]$Path,
        [string]$BackupRoot
    )

    if (-not (Test-Path $Path)) {
        return
    }

    $fileName = Split-Path -Leaf $Path
    Copy-Item -Path $Path -Destination (Join-Path $BackupRoot $fileName) -Force
}

$projectDirectory = Split-Path -Parent $ProjectPath
$appOfflinePath = Join-Path $TargetPath 'app_offline.htm'
$backupRoot = Join-Path $PublishPath '_backup'

Write-Step 'PDF.Service deployment checklist'
Write-Host ("ProjectPath             : {0}" -f $ProjectPath)
Write-Host ("TargetPath              : {0}" -f $TargetPath)
Write-Host ("PublishPath             : {0}" -f $PublishPath)
Write-Host ("PublicServiceBaseUrl    : {0}" -f $PublicServiceBaseUrl)

if (-not (Test-Path $ProjectPath)) {
    throw "Project file not found: $ProjectPath"
}

Write-Step 'Publishing PDF.Service'
if (Test-Path $PublishPath) {
    Remove-Item -Recurse -Force $PublishPath
}

dotnet publish $ProjectPath -c Release -o $PublishPath
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet publish failed.'
}

Write-Step 'Preparing backup of deployed configuration files'
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
Backup-FileIfExists -Path (Join-Path $TargetPath 'appsettings.json') -BackupRoot $backupRoot
Backup-FileIfExists -Path (Join-Path $TargetPath 'appsettings.Development.json') -BackupRoot $backupRoot
Backup-FileIfExists -Path (Join-Path $TargetPath 'web.config') -BackupRoot $backupRoot

Write-Step 'Taking PDF.Service offline for file replacement'
New-Item -ItemType Directory -Path $TargetPath -Force | Out-Null
Set-Content -Path $appOfflinePath -Value '<html><body>PDF.Service deployment in progress.</body></html>' -Encoding UTF8

try {
    Write-Step 'Copying published PDF.Service to IIS target via robocopy'
    & robocopy $PublishPath $TargetPath /MIR /R:2 /W:1 /XF app_offline.htm appsettings.json appsettings.Development.json
    if ($LASTEXITCODE -gt 7) {
        throw "robocopy failed with exit code $LASTEXITCODE"
    }
}
finally {
    if (Test-Path $appOfflinePath) {
        Remove-Item $appOfflinePath -Force
    }
}

Write-Step 'Waiting for application pool to restart'
Start-Sleep -Seconds 3

if (-not $SkipHealthCheck) {
    Write-Step 'Verifying PDF.Service health'
    $base = $PublicServiceBaseUrl.TrimEnd('/')
    $mergeStampUrl = "$base/api/Pdf/merge-stamp"
    
    if (Test-ServiceHealth -Url $mergeStampUrl -Label 'PDF Service merge-stamp' -AcceptedStatusCodes @(405)) {
        Write-Host "[OK] PDF.Service is responsive" -ForegroundColor Green
    } else {
        Write-Host "[WARN] PDF.Service health check inconclusive - verify manually" -ForegroundColor Yellow
    }
}

Write-Step 'PDF.Service deploy completed successfully'
