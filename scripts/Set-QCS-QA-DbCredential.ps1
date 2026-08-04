<#!
.SYNOPSIS
    Securely sets the QCS QA API database credential on the QA web server.

.DESCRIPTION
    Prompts the operator locally for the SQL login name and password, tests the
    credential against the QA database server, updates the QA server's
    appsettings.json directly, and restarts the QCS API app pool.

    Secrets are never written to the repository, echoed to the console, or
    stored outside the target server's appsettings.json.

.NOTES
    Run from an interactive PowerShell session on a trusted machine.
    Example:

        .\scripts\Set-QCS-QA-DbCredential.ps1

    Optional deploy credential:

        .\scripts\Set-QCS-QA-DbCredential.ps1 -ServerCredential (Get-Credential)
#>

[CmdletBinding()]
param(
    [string]$WebServerHost = 'AP-NTC2138-QAWB',
    [string]$DatabaseServer = '10.10.143.37',
    [string]$DatabaseName = 'QCS',
    [string]$ConfigPath = 'C:\inetpub\wwwroot\QCS\Service\appsettings.json',
    [string]$AppPoolName = 'QCS-Api-Pool',
    [PSCredential]$ServerCredential,
    [string]$DatabaseUserName
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Resolve-ServerCredential {
    param([PSCredential]$Credential)

    if ($null -ne $Credential) {
        return $Credential
    }

    $qrsCredentialHelper = 'C:\Users\n4734\source\TODO\QRS\scripts\QrsDeployCredential.ps1'
    if (Test-Path $qrsCredentialHelper) {
        . $qrsCredentialHelper
        return Resolve-QrsDeployCredential
    }

    return Get-Credential -Message "Enter the Windows credential for $WebServerHost"
}

function Convert-ToPlainText {
    param([Security.SecureString]$SecureString)

    return [PSCredential]::new('ignored', $SecureString).GetNetworkCredential().Password
}

if ([string]::IsNullOrWhiteSpace($DatabaseUserName)) {
    $DatabaseUserName = Read-Host 'QA SQL login name'
}

if ([string]::IsNullOrWhiteSpace($DatabaseUserName)) {
    throw 'Database user name is required.'
}

$databasePasswordSecure = Read-Host 'QA SQL password' -AsSecureString
$databasePassword = Convert-ToPlainText -SecureString $databasePasswordSecure
$ServerCredential = Resolve-ServerCredential -Credential $ServerCredential

$session = $null
try {
    $session = New-PSSession -ComputerName $WebServerHost -Credential $ServerCredential

    Invoke-Command -Session $session -ArgumentList $ConfigPath, $AppPoolName, $DatabaseServer, $DatabaseName, $DatabaseUserName, $databasePassword -ScriptBlock {
        param(
            $RemoteConfigPath,
            $RemoteAppPoolName,
            $RemoteDatabaseServer,
            $RemoteDatabaseName,
            $RemoteDatabaseUserName,
            $RemoteDatabasePassword
        )

        $ErrorActionPreference = 'Stop'
        Set-StrictMode -Version Latest
        Add-Type -AssemblyName System.Data
        Import-Module WebAdministration

        if (-not (Test-Path $RemoteConfigPath)) {
            throw "QCS configuration file was not found: $RemoteConfigPath"
        }

        $configDirectory = Split-Path -Parent $RemoteConfigPath
        $backupDirectory = Join-Path $configDirectory '_backup'
        New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null

        $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        $backupPath = Join-Path $backupDirectory ("appsettings.json.$timestamp.bak")
        $originalText = Get-Content $RemoteConfigPath -Raw
        Copy-Item -Path $RemoteConfigPath -Destination $backupPath -Force

        $connectionString = "Server=$RemoteDatabaseServer;Database=$RemoteDatabaseName;User ID=$RemoteDatabaseUserName;Password=$RemoteDatabasePassword;TrustServerCertificate=True;MultipleActiveResultSets=True"

        $configChanged = $false
        try {
            $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
            try {
                $connection.Open()
                $command = $connection.CreateCommand()
                $command.CommandText = 'SELECT DB_NAME()'
                $connectedDatabase = [string]$command.ExecuteScalar()
                if ($connectedDatabase -ne $RemoteDatabaseName) {
                    throw "Credential connected to unexpected database '$connectedDatabase'."
                }
            }
            finally {
                $connection.Dispose()
            }

            $config = $originalText | ConvertFrom-Json
            if ($null -eq $config.ConnectionStrings) {
                $config | Add-Member -MemberType NoteProperty -Name ConnectionStrings -Value ([pscustomobject]@{ DefaultConnection = '' })
            }

            $config.ConnectionStrings.DefaultConnection = $connectionString
            $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
            [IO.File]::WriteAllText($RemoteConfigPath, ($config | ConvertTo-Json -Depth 20), $utf8NoBom)
            $configChanged = $true

            Restart-WebAppPool -Name $RemoteAppPoolName
            $deadline = [DateTime]::UtcNow.AddSeconds(30)
            do {
                $poolState = (Get-WebAppPoolState -Name $RemoteAppPoolName).Value
                if ($poolState -eq 'Started') {
                    break
                }
            }
            while ([DateTime]::UtcNow -lt $deadline)

            if ($poolState -ne 'Started') {
                throw "App pool $RemoteAppPoolName did not return to Started."
            }

            [pscustomobject]@{
                Server = $env:COMPUTERNAME
                DatabaseServer = $RemoteDatabaseServer
                Database = $RemoteDatabaseName
                AppPool = $RemoteAppPoolName
                PoolState = $poolState
                BackupPath = $backupPath
                CredentialStored = $true
            }
        }
        catch {
            if ($configChanged) {
                [IO.File]::WriteAllText($RemoteConfigPath, $originalText, (New-Object System.Text.UTF8Encoding($false)))
                Restart-WebAppPool -Name $RemoteAppPoolName -ErrorAction SilentlyContinue
            }

            throw
        }
    } | Format-List
}
finally {
    $databasePassword = $null
    $databasePasswordSecure = $null

    if ($null -ne $session) {
        Remove-PSSession -Session $session -ErrorAction SilentlyContinue
    }
}