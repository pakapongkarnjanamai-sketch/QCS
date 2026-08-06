[CmdletBinding()]
param(
    [PSCredential]$Credential,
    [string]$Path = (Join-Path $env:LOCALAPPDATA 'QCS\deploy-credential.clixml')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($null -eq $Credential) {
    $Credential = Get-Credential -Message 'Enter the QA deployment account'
}
if ($null -eq $Credential) {
    throw 'A QA deployment credential is required.'
}

$directory = Split-Path $Path -Parent
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$Credential | Export-Clixml -Path $Path -Force
Write-Host "Saved the DPAPI-protected QCS deploy credential to $Path"