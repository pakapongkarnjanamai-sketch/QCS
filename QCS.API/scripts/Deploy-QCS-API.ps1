[CmdletBinding()]
param(
    [string]$ProjectPath = 'c:\Users\n4734\source\repos\QCS\QCS.API\QCS.API.csproj',
    [string]$PublishPath = 'c:\Users\n4734\source\repos\QCS\artifacts\publish\QCS.API',
    [Parameter(Mandatory)]
    [ValidateSet('QA')]
    [string]$Environment,
    [Parameter(Mandatory)]
    [string]$ServerHost,
    [Parameter(Mandatory)]
    [string]$TargetPath,
    [Parameter(Mandatory)]
    [string]$PublicApiBaseUrl,
    [PSCredential]$Credential,
    [string]$CredentialPath = (Join-Path $env:LOCALAPPDATA 'QCS\deploy-credential.clixml'),
    [switch]$SkipSmokeTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Write-Step {
    param([string]$Message)

    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Resolve-DeployCredential {
    param(
        [PSCredential]$SuppliedCredential,
        [string]$Path
    )

    if ($null -ne $SuppliedCredential) {
        return $SuppliedCredential
    }
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "QCS deploy credential is not configured. Run Save-QCS-DeployCredential.ps1 or pass -Credential."
    }

    $cachedCredential = Import-Clixml -LiteralPath $Path
    if ($cachedCredential -isnot [PSCredential]) {
        throw "QCS deploy credential at '$Path' is invalid. Save it again."
    }

    return $cachedCredential
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

Write-Step 'API deployment checklist'
Write-Host ("Environment      : {0}" -f $Environment)
Write-Host ("ServerHost      : {0}" -f $ServerHost)
Write-Host ("ProjectPath      : {0}" -f $ProjectPath)
Write-Host ("TargetPath       : {0}" -f $TargetPath)
Write-Host ("PublishPath      : {0}" -f $PublishPath)
Write-Host ("PublicApiBaseUrl : {0}" -f $PublicApiBaseUrl)

$qaServerHost = 'ap-ntc2138-qawb'
$expectedTargetPath = "\\$qaServerHost\wwwroot\QCS\Service"
$publicUri = $null
if ($Environment -ne 'QA') {
    throw "Only the QA environment is allowed. Resolved environment: '$Environment'."
}
if (-not [string]::Equals($ServerHost, $qaServerHost, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Only QA host '$qaServerHost' is allowed. Resolved host: '$ServerHost'."
}
if (-not [string]::Equals($TargetPath.TrimEnd('\'), $expectedTargetPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Only QA target '$expectedTargetPath' is allowed. Resolved target: '$TargetPath'."
}
if (-not [Uri]::TryCreate($PublicApiBaseUrl, [UriKind]::Absolute, [ref]$publicUri) -or
    $publicUri.Scheme -ne 'https' -or
    -not [string]::Equals($publicUri.Host, $qaServerHost, [StringComparison]::OrdinalIgnoreCase) -or
    $publicUri.AbsolutePath.TrimEnd('/') -ine '/QCS/Service') {
    throw "PublicApiBaseUrl must be the QA QCS Service URL on '$qaServerHost'."
}
if ($ProjectPath.StartsWith('\\') -or $PublishPath.StartsWith('\\')) {
    throw 'ProjectPath and PublishPath must be local paths.'
}

if (-not (Test-Path $ProjectPath)) {
    throw "Project file not found: $ProjectPath"
}

$Credential = Resolve-DeployCredential -SuppliedCredential $Credential -Path $CredentialPath
$appOfflinePath = Join-Path $TargetPath 'app_offline.htm'
$backupRoot = Join-Path $PublishPath '_backup'
$appPoolName = 'QCS-Api-Pool'

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

$session = $null
$restartAppPool = $false
try {
    $session = New-PSSession -ComputerName $ServerHost -Credential $Credential
    $currentPoolState = Invoke-Command -Session $session -ArgumentList $appPoolName -ScriptBlock {
        param($PoolName)
        Import-Module WebAdministration
        (Get-WebAppPoolState -Name $PoolName).Value
    }

    if ($currentPoolState -ne 'Stopped') {
        Write-Step "Stopping QA IIS app pool '$appPoolName'"
        $restartAppPool = $true
        Invoke-Command -Session $session -ArgumentList $appPoolName -ScriptBlock {
            param($PoolName)
            Import-Module WebAdministration
            Stop-WebAppPool -Name $PoolName
            $state = (Get-WebAppPoolState -Name $PoolName).Value
            if ($state -ne 'Stopped') {
                throw "App pool '$PoolName' did not stop. Current state: $state."
            }
        }
    }

    Write-Step 'Taking API offline for file replacement'
    New-Item -ItemType Directory -Path $TargetPath -Force | Out-Null
    Set-Content -Path $appOfflinePath -Value '<html><body>QCS API deployment in progress.</body></html>' -Encoding UTF8

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
    if ($restartAppPool -and $null -ne $session -and $session.State -eq 'Opened') {
        try {
            Write-Step "Starting QA IIS app pool '$appPoolName'"
            Invoke-Command -Session $session -ArgumentList $appPoolName -ScriptBlock {
                param($PoolName)
                Import-Module WebAdministration
                Start-WebAppPool -Name $PoolName
            }
        }
        catch {
            Write-Error "Failed to restart QA app pool '$appPoolName': $_"
        }
    }
    if ($null -ne $session) {
        Remove-PSSession $session -ErrorAction SilentlyContinue
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