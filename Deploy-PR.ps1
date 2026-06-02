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

# 1. Deploy QCS.Web.User (Parent Root Application)
Write-Header "1/4 Deploying QCS.Web.User (MVC Web Portal) to Production"
& "$Root\QCS.Web.User\scripts\Deploy-QCS-Web-User.ps1" `
    -TargetPath "\\10.10.154.21\wwwroot\QCS" `
    -PublicWebBaseUrl "https://ap-ntc2137-prwb/QCS" `
    -Environment Production `
    -SkipSmokeTest:$SkipSmokeTest

# 2. Deploy QCS.API (Backend API Sub-Application)
Write-Header "2/4 Deploying QCS.API (REST API Backend) to Production"
& "$Root\QCS.API\scripts\Deploy-QCS-API.ps1" `
    -TargetPath "\\10.10.154.21\wwwroot\QCS\Service" `
    -PublicApiBaseUrl "https://ap-ntc2137-prwb/QCS/Service" `
    -Environment Production `
    -SkipSmokeTest:$SkipSmokeTest

# 3. Deploy PDF.Service (Document Rendering Service)
Write-Header "3/4 Deploying PDF.Service to Production"
& "$Root\PDF.Service\scripts\Deploy-PDF-Service.ps1" `
    -TargetPath "\\10.10.154.21\wwwroot\QCS\PDF" `
    -PublicServiceBaseUrl "http://ap-ntc2137-prwb/QCS/PDF" `
    -SkipHealthCheck:$SkipSmokeTest

# 4. Deploy QCS.React.Admin (Static SPA Sub-Application)
Write-Header "4/4 Deploying QCS.React.Admin (Vite/React SPA) to Production"
& "$Root\QCS.React.Admin\scripts\Deploy-QCS-React-Admin.ps1" `
    -TargetPath "\\10.10.154.21\wwwroot\QCS\Admin" `
    -PublicBasePath "/QCS/admin" `
    -ApiBaseUrl "/QCS/Service" `
    -HubUrl "/QCS/Service/hubs/qcs" `
    -PortalBaseUrl "/QCS" `
    -PublicSiteOrigin "https://ap-ntc2137-prwb" `
    -SkipSmokeTest:$SkipSmokeTest

Write-Host "`n>>> All QCS services successfully deployed to Production! <<<`n" -ForegroundColor Green
