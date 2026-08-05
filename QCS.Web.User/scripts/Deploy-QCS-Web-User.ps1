[CmdletBinding()]
param(
    [string]$ProjectPath = 'c:\Users\n4734\source\repos\QCS\QCS.Web.User\QCS.Web.User.csproj',
    [string]$TargetPath = '\\10.10.154.21\wwwroot\QCS',
    [string]$PublishPath = 'c:\Users\n4734\source\repos\QCS\artifacts\publish\QCS.Web.User',
    [string]$PublicWebBaseUrl = 'https://ap-ntc2137-prwb/QCS',
    [string]$Environment = 'Production',
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
            # Use native Windows curl.exe to bypass SSL issues and TLS protocol constraints under PowerShell 5.1
            $statusString = & "curl.exe" -k -s -w "%{http_code}" -o "NUL" --negotiate -u ":" $Url
            $statusCode = [int]$statusString

            if ($statusCode -ge 200 -and $statusCode -lt 400) {
                Write-Host ("{0}: {1} ({2})" -f $Label, $statusCode, $Url) -ForegroundColor Green
                return
            }

            if ($statusCode -eq 401 -or $statusCode -eq 405) {
                Write-Host ("{0}: {1} ({2}) [Warning: Service is Responsive]" -f $Label, $statusCode, $Url) -ForegroundColor Yellow
                return
            }

            throw "$Label failed with status ${statusCode}: $Url"
        }
        catch {
            if ($attempt -eq $MaxAttempts) {
                throw
            }
            Start-Sleep -Seconds 2
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

Write-Step 'Web User deployment checklist'
Write-Host ("ProjectPath      : {0}" -f $ProjectPath)
Write-Host ("TargetPath       : {0}" -f $TargetPath)
Write-Host ("PublishPath      : {0}" -f $PublishPath)
Write-Host ("PublicWebBaseUrl : {0}" -f $PublicWebBaseUrl)

if (-not (Test-Path $ProjectPath)) {
    throw "Project file not found: $ProjectPath"
}

Write-Step 'Publishing QCS.Web.User'
if (Test-Path $PublishPath) {
    Remove-Item -Recurse -Force $PublishPath
}

dotnet publish $ProjectPath -c Release -o $PublishPath
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet publish failed.'
}

Write-Step 'Configuring published web.config environment and diagnostics'
$publishedWebConfig = Join-Path $PublishPath 'web.config'
if (Test-Path $publishedWebConfig) {
    $xml = [xml](Get-Content -Path $publishedWebConfig -Raw)
    $aspNetCoreNode = $xml.SelectSingleNode("//aspNetCore")
    if ($aspNetCoreNode) {
        # Enable stdout logging for diagnostics
        $aspNetCoreNode.SetAttribute("stdoutLogEnabled", "true")

        # Check if environmentVariables node already exists
        $envVarsNode = $aspNetCoreNode.SelectSingleNode("environmentVariables")
        if (-not $envVarsNode) {
            $envVarsNode = $xml.CreateElement("environmentVariables")
            $aspNetCoreNode.AppendChild($envVarsNode) | Out-Null
        }

        # Add ASPNETCORE_ENVIRONMENT
        $envVarNode = $xml.CreateElement("environmentVariable")
        $envVarNode.SetAttribute("name", "ASPNETCORE_ENVIRONMENT")
        $envVarNode.SetAttribute("value", $Environment)
        $envVarsNode.AppendChild($envVarNode) | Out-Null

        $xml.Save($publishedWebConfig)
        Write-Host "Injected ASPNETCORE_ENVIRONMENT=$Environment and enabled stdout logging in published web.config" -ForegroundColor DarkCyan
    }
}

Write-Step 'Preparing backup of deployed appsettings'
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
Backup-FileIfExists -Path (Join-Path $TargetPath 'appsettings.json') -BackupRoot $backupRoot
Backup-FileIfExists -Path (Join-Path $TargetPath 'appsettings.QA.json') -BackupRoot $backupRoot
Backup-FileIfExists -Path (Join-Path $TargetPath 'web.config') -BackupRoot $backupRoot

Write-Step 'Taking Web User offline for file replacement'
New-Item -ItemType Directory -Path $TargetPath -Force | Out-Null
Set-Content -Path $appOfflinePath -Value '<html><body>QCS Web User deployment in progress.</body></html>' -Encoding UTF8

try {
    Write-Step 'Copying published Web User to IIS target via robocopy'
    # Exclude nested applications (Service, Admin, PDF) from MIR deletion so they are not wiped!
    & robocopy $PublishPath $TargetPath /MIR /R:2 /W:1 /XF app_offline.htm appsettings.json appsettings.Development.json appsettings.QA.json /XD Service Admin PDF _backup logs
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
    Write-Step 'Running Web User smoke tests'
    $base = $PublicWebBaseUrl.TrimEnd('/')
    Invoke-CheckedRequest -Url "$base/?view=all-approved" -Label 'Web User Approved Page'
    Invoke-CheckedRequest -Url "$base/Home/Index" -Label 'Web User Home Index'
}

Write-Step 'Web User deploy completed successfully'
