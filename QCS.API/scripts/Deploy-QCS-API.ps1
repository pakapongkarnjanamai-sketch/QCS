[CmdletBinding()]
param(
    [string]$ProjectPath = 'c:\Users\n4734\source\repos\QCS\QCS.API\QCS.API.csproj',
    [string]$TargetPath = '\\10.10.154.21\wwwroot\QCS\Service',
    [string]$PublishPath = 'c:\Users\n4734\source\repos\QCS\artifacts\publish\QCS.API',
    [string]$PublicApiBaseUrl = 'https://ap-ntc2137-prwb/QCS/Service',
    [string]$Environment = 'Production',
    [switch]$SkipSmokeTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Step {
    param([string]$Message)

    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

# Invoke-CheckedRequest was deleted on 2026-08-06. It counted 401 as success ("Service is
# Responsive"), which made the suite report green through a PROD outage. Smoke checks now live in
# Test-QCS-ApiSmoke.ps1, which asserts the status each endpoint is supposed to return.

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
Backup-FileIfExists -Path (Join-Path $TargetPath 'appsettings.Development.json') -BackupRoot $backupRoot
Backup-FileIfExists -Path (Join-Path $TargetPath 'web.config') -BackupRoot $backupRoot

Write-Step 'Taking API offline for file replacement'
New-Item -ItemType Directory -Path $TargetPath -Force | Out-Null
Set-Content -Path $appOfflinePath -Value '<html><body>QCS API deployment in progress.</body></html>' -Encoding UTF8

try {
    Write-Step 'Copying published API to IIS target via robocopy'
    & robocopy $PublishPath $TargetPath /MIR /R:2 /W:1 /XF app_offline.htm appsettings.json appsettings.Development.json appsettings.QA.json /XD logs
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

    # The suite lives in Test-QCS-ApiSmoke.ps1 so it can also be run on its own against any
    # environment, without deploying to it. It throws on failure, and $ErrorActionPreference is
    # Stop, so a failed check fails the deploy.
    & (Join-Path $PSScriptRoot 'Test-QCS-ApiSmoke.ps1') -BaseUrl $PublicApiBaseUrl
}

Write-Step 'API deploy completed successfully'