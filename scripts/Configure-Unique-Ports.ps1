# c:\Users\n4734\source\repos\QCS\scripts\Configure-Unique-Ports.ps1
# Finds free ports and configures all projects in the QCS solution to use them persistently.

$ErrorActionPreference = 'Stop'

function Get-SevenFreePorts {
    $properties = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties()
    $connections = $properties.GetActiveTcpConnections()
    $listeners = $properties.GetActiveTcpListeners()
    
    $usedPorts = @()
    foreach ($c in $connections) { $usedPorts += $c.LocalEndPoint.Port }
    foreach ($l in $listeners) { $usedPorts += $l.Port }
    
    $allocated = @()
    
    function Get-NextFree {
        param($start, $end)
        $portRange = $start..$end
        $shuffled = Get-Random -InputObject $portRange -Count $portRange.Length
        foreach ($port in $shuffled) {
            if ($usedPorts -notcontains $port -and $allocated -notcontains $port) {
                # Add to allocated in the outer scope
                $script:allocated += $port
                return $port
            }
        }
        throw "No free ports found in range $start to $end"
    }
    
    $apiHttp = Get-NextFree 5001 6000
    $apiHttps = Get-NextFree 7001 8000
    
    $webHttp = Get-NextFree 5001 6000
    $webHttps = Get-NextFree 7001 8000
    
    $pdfHttp = Get-NextFree 5001 6000
    $pdfHttps = Get-NextFree 7001 8000
    
    $react = Get-NextFree 5001 6000
    
    return [PSCustomObject]@{
        ApiHttp = $apiHttp
        ApiHttps = $apiHttps
        WebHttp = $webHttp
        WebHttps = $webHttps
        PdfHttp = $pdfHttp
        PdfHttps = $pdfHttps
        React = $react
    }
}

$ports = Get-SevenFreePorts
$allocated = $null # clear variable references

Write-Host "=== Configuring QCS Unique Ports ===" -ForegroundColor Cyan
Write-Host "QCS.API HTTP Port       -> $($ports.ApiHttp)"
Write-Host "QCS.API HTTPS Port      -> $($ports.ApiHttps)"
Write-Host "PDF.Service HTTP Port   -> $($ports.PdfHttp)"
Write-Host "PDF.Service HTTPS Port  -> $($ports.PdfHttps)"
Write-Host "QCS.React.Admin Port    -> $($ports.React)"
Write-Host "===================================="

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot ".."))

# 1. Update QCS.API launchSettings.json
$apiLaunchPath = Join-Path $projectRoot "QCS.API/Properties/launchSettings.json"
if (Test-Path $apiLaunchPath) {
    $content = Get-Content $apiLaunchPath -Raw
    $content = $content -replace '"applicationUrl":\s*"https://localhost:\d+;http://localhost:\d+"', "`"applicationUrl`": `"https://localhost:$($ports.ApiHttps);http://localhost:$($ports.ApiHttp)`""
    Set-Content -Path $apiLaunchPath -Value $content -Encoding UTF8
    Write-Host "Updated QCS.API launchSettings.json successfully." -ForegroundColor Green
}

# QCS.Web.User's launchSettings step was removed with the project in PLAN-051 Phase 6. WebHttp and
# WebHttps are still allocated above so the port arithmetic and any saved local settings keep their
# existing offsets; they are simply no longer written anywhere.

# 2. Update PDF.Service launchSettings.json
$pdfLaunchPath = Join-Path $projectRoot "PDF.Service/Properties/launchSettings.json"
if (Test-Path $pdfLaunchPath) {
    $content = Get-Content $pdfLaunchPath -Raw
    $content = $content -replace '"applicationUrl":\s*"https://localhost:\d+;http://localhost:\d+"', "`"applicationUrl`": `"https://localhost:$($ports.PdfHttps);http://localhost:$($ports.PdfHttp)`""
    Set-Content -Path $pdfLaunchPath -Value $content -Encoding UTF8
    Write-Host "Updated PDF.Service launchSettings.json successfully." -ForegroundColor Green
}

# 3. Update QCS.API appsettings.Development.json (CORS & PDF service url)
$apiAppsettingsDevPath = Join-Path $projectRoot "QCS.API/appsettings.Development.json"
if (Test-Path $apiAppsettingsDevPath) {
    $content = Get-Content $apiAppsettingsDevPath -Raw
    # Update React Admin ports
    $content = $content -replace '"http://localhost:\d+"', "`"http://localhost:$($ports.React)`""
    $content = $content -replace '"http://127.0.0.1:\d+"', "`"http://127.0.0.1:$($ports.React)`""
    # The two Web.User CORS rewrites that followed were removed with the project. They were also a
    # bug: the first re-matched the same '"http://localhost:\d+"' pattern the React line had just
    # written, so the React origin was immediately overwritten with the MVC port.
    # Update PDF Service URL
    $content = $content -replace '"PdfServiceUrl":\s*"https://localhost:\d+"', "`"PdfServiceUrl`": `"https://localhost:$($ports.PdfHttps)`""
    Set-Content -Path $apiAppsettingsDevPath -Value $content -Encoding UTF8
    Write-Host "Updated QCS.API appsettings.Development.json successfully." -ForegroundColor Green
}

# 6. Update QCS.React.Admin/.env
$reactEnvPath = Join-Path $projectRoot "QCS.React.Admin/.env"
$reactEnvExamplePath = Join-Path $projectRoot "QCS.React.Admin/.env.example"
if (-not (Test-Path $reactEnvPath) -and (Test-Path $reactEnvExamplePath)) {
    Copy-Item $reactEnvExamplePath $reactEnvPath
    Write-Host "Copied .env.example to .env in QCS.React.Admin." -ForegroundColor Yellow
}
if (Test-Path $reactEnvPath) {
    $content = Get-Content $reactEnvPath -Raw
    $content = $content -replace 'VITE_QCS_API_BASE_URL=https://localhost:\d+', "VITE_QCS_API_BASE_URL=https://localhost:$($ports.ApiHttps)"
    $content = $content -replace 'VITE_QCS_HUB_URL=https://localhost:\d+/hubs/qcs', "VITE_QCS_HUB_URL=https://localhost:$($ports.ApiHttps)/hubs/qcs"
    Set-Content -Path $reactEnvPath -Value $content -Encoding UTF8
    Write-Host "Updated QCS.React.Admin/.env successfully." -ForegroundColor Green
}

# 7. Update .vscode/launch.json
$launchPath = Join-Path $projectRoot ".vscode/launch.json"
if (Test-Path $launchPath) {
    $content = Get-Content $launchPath -Raw
    # Update npm run dev port command for QCS.React.Admin configuration
    $content = $content -replace '"command":\s*"npm run dev -- --host localhost --port \d+ --strictPort"', "`"command`": `"npm run dev -- --host localhost --port $($ports.React) --strictPort`""
    Set-Content -Path $launchPath -Value $content -Encoding UTF8
    Write-Host "Updated QCS .vscode/launch.json successfully." -ForegroundColor Green
}

Write-Host "Done! All QCS ports have been randomized and updated." -ForegroundColor Green
