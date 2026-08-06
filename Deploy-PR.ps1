# =============================================================================================
# THIS WRAPPER NO LONGER RUNS, AND THAT IS DELIBERATE.
#
# PLAN-051 removed QCS.Web.User and moved the request lifecycle onto the central GPCS Approval
# service. PROD still runs the old application against the old schema, so this file no longer
# describes anything that can be deployed:
#
#   * the MVC step below called a script that has been deleted;
#   * Deploy-QCS-API.ps1 now accepts -Environment QA only, by design, after an accidental PROD
#     deploy on 2026-08-06 took the service down;
#   * PROD needs the EF migration, a QRS release carrying the new status contract, and a workflow
#     definition published in the PROD Approval service — in that order.
#
# The PROD cutover is a separate, reviewed, human-only plan. It is not this script, and no agent
# executes it. The file is kept rather than deleted because its target paths and ordering are
# useful input to that plan.
#
# Nothing below this guard will run. Remove the guard only as part of the PROD cutover plan.
# =============================================================================================
[CmdletBinding()]
param(
    [switch]$SkipSmokeTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

throw 'Deploy-PR.ps1 is disabled. The PROD cutover for PLAN-051 is a reviewed human-only procedure; see DOC/PLANS in the QRS repository.'

$Root = 'c:\Users\n4734\source\repos\QCS'

function Write-Header {
    param([string]$Message)
    Write-Host "`n===============================================" -ForegroundColor Yellow
    Write-Host ">>> $Message" -ForegroundColor Yellow
    Write-Host "===============================================" -ForegroundColor Yellow
}

# The MVC portal step that stood here is gone with QCS.Web.User. /QCS becomes a static one-hop
# redirect to /QCS/User, provisioned once by the IIS setup script rather than deployed per release.

# 1. Deploy QCS.API (Backend API Sub-Application)
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
