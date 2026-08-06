[CmdletBinding()]
param(
    [switch]$SkipSmokeTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Root = 'c:\Users\n4734\source\repos\QCS'

function Write-Header {
    param([string]$Message)
    Write-Host "`n===============================================" -ForegroundColor Yellow
    Write-Host ">>> $Message" -ForegroundColor Yellow
    Write-Host "===============================================" -ForegroundColor Yellow
}

# The MVC portal step used to be first here. QCS.Web.User was removed in PLAN-051 Phase 6 and
# QCS.React.User is now the only user portal; /QCS serves a static one-hop redirect to /QCS/User,
# provisioned by scripts/Setup-QCS-QA-IIS.ps1 rather than deployed on every release.

# 1. Deploy QCS.API (Backend API Sub-Application)
Write-Header "1/4 Deploying QCS.API (REST API Backend) to QA"
# -ServerHost is mandatory since PLAN-052 hardened this script; without it the deploy stops on a
# prompt rather than running. Every target is named explicitly here — there are no defaults left.
& "$Root\QCS.API\scripts\Deploy-QCS-API.ps1" `
    -Environment QA `
    -ServerHost "AP-NTC2138-QAWB" `
    -TargetPath "\\AP-NTC2138-QAWB\wwwroot\QCS\Service" `
    -PublicApiBaseUrl "https://ap-ntc2138-qawb/QCS/Service" `
    -SkipSmokeTest:$SkipSmokeTest

# 2. Deploy PDF.Service (Document Rendering Service)
Write-Header "2/4 Deploying PDF.Service to QA"
& "$Root\PDF.Service\scripts\Deploy-PDF-Service.ps1" `
    -TargetPath "\\10.10.143.39\wwwroot\QCS\PDF" `
    -PublicServiceBaseUrl "http://ap-ntc2138-qawb/QCS/PDF" `
    -SkipHealthCheck:$SkipSmokeTest

# 3. Deploy QCS.React.Admin (Static SPA Sub-Application)
Write-Header "3/4 Deploying QCS.React.Admin (Vite/React SPA) to QA"
& "$Root\QCS.React.Admin\scripts\Deploy-QCS-React-Admin.ps1" `
    -TargetPath "\\10.10.143.39\wwwroot\QCS\Admin" `
    -PublicBasePath "/QCS/admin" `
    -ApiBaseUrl "/QCS/Service" `
    -HubUrl "/QCS/Service/hubs/qcs" `
    -PortalBaseUrl "/QCS" `
    -PublicSiteOrigin "https://ap-ntc2138-qawb" `
    -SkipSmokeTest:$SkipSmokeTest

# 4. Deploy QCS.React.User (Static SPA Sub-Application)
Write-Header "4/4 Deploying QCS.React.User (Vite/React SPA) to QA"
& "$Root\QCS.React.User\scripts\Deploy-QCS-React-User.ps1" `
    -TargetPath "\\10.10.143.39\wwwroot\QCS\User" `
    -PublicBasePath "/QCS/User" `
    -ApiBaseUrl "/QCS/Service" `
    -HubUrl "/QCS/Service/notificationHub" `
    -LegacyPortalBaseUrl "/QCS" `
    -PublicSiteOrigin "https://ap-ntc2138-qawb" `
    -SkipSmokeTest:$SkipSmokeTest

Write-Host "`n>>> All QCS services successfully deployed to QA! <<<`n" -ForegroundColor Green
