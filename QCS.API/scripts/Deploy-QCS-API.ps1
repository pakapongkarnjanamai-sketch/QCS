[CmdletBinding()]
param(
    [string]$ProjectPath = 'c:\Users\n4734\source\repos\QCS\QCS.API\QCS.API.csproj',
    [string]$TargetPath = '\\10.10.154.21\wwwroot\QCS\Service',
    [string]$PublishPath = 'c:\Users\n4734\source\repos\QCS\artifacts\publish\QCS.API',
    [string]$PublicApiBaseUrl = 'https://ap-ntc2137-prwb/QCS/Service',
    [switch]$SkipSmokeTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Step {
    param([string]$Message)

    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Invoke-CheckedRequest {
    param(
        [string]$Url,
        [string]$Label,
        [int]$MaxAttempts = 3
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseDefaultCredentials -SkipCertificateCheck -SkipHttpErrorCheck
            if ($response.StatusCode -ge 400) {
                throw "$Label failed with status $($response.StatusCode): $Url"
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

Write-Step 'API deployment checklist'
Write-Host ("ProjectPath      : {0}" -f $ProjectPath)
Write-Host ("TargetPath       : {0}" -f $TargetPath)
Write-Host ("PublishPath      : {0}" -f $PublishPath)
Write-Host ("PublicApiBaseUrl : {0}" -f $PublicApiBaseUrl)

if (-not (Test-Path $ProjectPath)) {
    throw "Project file not found: $ProjectPath"
}

Write-Step 'Publishing QCS.API'
if (Test-Path $PublishPath) {
    Remove-Item -Recurse -Force $PublishPath
}

dotnet publish $ProjectPath -c Release -o $PublishPath
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet publish failed.'
}

Write-Step 'Preparing backup of deployed appsettings'
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
Backup-FileIfExists -Path (Join-Path $TargetPath 'appsettings.json') -BackupRoot $backupRoot
Backup-FileIfExists -Path (Join-Path $TargetPath 'appsettings.Development.json') -BackupRoot $backupRoot
Backup-FileIfExists -Path (Join-Path $TargetPath 'web.config') -BackupRoot $backupRoot

Write-Step 'Taking API offline for file replacement'
New-Item -ItemType Directory -Path $TargetPath -Force | Out-Null
Set-Content -Path $appOfflinePath -Value '<html><body>QCS API deployment in progress.</body></html>' -Encoding UTF8

try {
    Write-Step 'Copying published API to IIS target via robocopy'
    & robocopy $PublishPath $TargetPath /MIR /R:2 /W:1 /XF app_offline.htm
    if ($LASTEXITCODE -gt 7) {
        throw "robocopy failed with exit code $LASTEXITCODE"
    }
}
finally {
    if (Test-Path $appOfflinePath) {
        Remove-Item $appOfflinePath -Force
    }
}

if (-not $SkipSmokeTest) {
    Write-Step 'Running API smoke tests'
    $base = $PublicApiBaseUrl.TrimEnd('/')
    Invoke-CheckedRequest -Url "$base/api/Dashboard/Summary" -Label 'Dashboard summary'
    Invoke-CheckedRequest -Url "$base/api/Dashboard/RequesterTrend?days=7&top=5" -Label 'Requester trend'
    Invoke-CheckedRequest -Url "$base/api/Dashboard/ValidityStatus" -Label 'Validity status'
    Invoke-CheckedRequest -Url "$base/api/Dashboard/ActiveVendors?top=10" -Label 'Active vendors'
    Invoke-CheckedRequest -Url "$base/api/Request/Admin/All?skip=0&take=1&requireTotalCount=true" -Label 'Admin all requests'
    Invoke-CheckedRequest -Url "$base/api/Request/Admin/Requesters?skip=0&take=5&sort=%5B%7B%22selector%22%3A%22quotationCount%22%2C%22desc%22%3Atrue%7D%5D" -Label 'Admin requesters'
    Invoke-CheckedRequest -Url "$base/api/Dashboard/RequestTrend?timeframe=7d&aggregation=day" -Label 'Trend window'
    Invoke-CheckedRequest -Url "$base/api/Session/Me" -Label 'Session me'
}

Write-Step 'API deploy completed successfully'