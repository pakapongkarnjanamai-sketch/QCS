<#
.SYNOPSIS
    Post-deploy smoke tests for the QCS API.

.DESCRIPTION
    Written after the 2026-08-06 incident, where a deploy left every request-detail endpoint
    throwing on PROD and the smoke suite still reported green. Two things had gone wrong:

      1. The suite only called dashboard and list endpoints. Those project RequestGridDto and the
         dashboard DTOs, neither of which carried the columns the release had added, so the one
         DTO the release actually changed was never executed.
      2. Invoke-CheckedRequest counted 401 as success ("Service is Responsive"). That proves IIS
         is answering, not that the app works.

    So this script asserts three things per check, not one:

      * the status code is what that endpoint is SUPPOSED to return — 401 is a pass only where
        the endpoint is meant to reject us, and a failure everywhere else;
      * the body parses as JSON and carries the properties the callers depend on, which is what
        catches a projection that no longer matches the database;
      * the content type is right, which is what catches an IIS error page served as 200.

    Detail checks are chained: the suite reads a real code and id out of the list endpoints and
    feeds them to the detail endpoints, so it exercises live rows instead of a hardcoded id that
    goes stale. When a list comes back empty the dependent checks report SKIPPED — never passed.

    Runs standalone, so it is also the way to check an environment without deploying to it:

        .\Test-QCS-ApiSmoke.ps1 -BaseUrl https://ap-ntc2138-qawb/QCS/Service

.PARAMETER BaseUrl
    Root of the deployed API, e.g. https://ap-ntc2138-qawb/QCS/Service

.PARAMETER MaxAttempts
    Retries per check. An app pool that is still warming up returns 503 for a moment.

.PARAMETER Detailed
    Print the first 400 characters of every response body, including passing ones.

.OUTPUTS
    Exit code 0 when every check passed or skipped, 1 when any check failed.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,

    [int]$MaxAttempts = 3,

    # Code of a request that IS linked to QRS. Optional, because whether one exists is
    # environment-specific. Given one, the suite asserts sourceSystem/sourceCode really do come
    # back — the only way to check those, since nulls are omitted from responses (see the detail
    # section below). Without one, that assertion reports SKIPPED rather than silently passing.
    [string]$SourceLinkedCode,

    [switch]$Detailed
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$script:Results = New-Object System.Collections.ArrayList
$baseUri = $null
if (-not [Uri]::TryCreate($BaseUrl, [UriKind]::Absolute, [ref]$baseUri)) {
    throw "BaseUrl must be an absolute local or QA URL. Resolved value: '$BaseUrl'."
}
$allowedHosts = @('ap-ntc2138-qawb', 'localhost', '127.0.0.1')
if ($allowedHosts -notcontains $baseUri.Host.ToLowerInvariant()) {
    throw "Smoke tests are restricted to local hosts and ap-ntc2138-qawb. Resolved host: '$($baseUri.Host)'."
}
$base = $BaseUrl.TrimEnd('/')

function Write-Step {
    param([string]$Message)
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

<#
    Raw call. Body goes to a temp file rather than being captured from stdout, because the status
    code has to come back on stdout via -w and the two would otherwise be concatenated.
#>
function Invoke-RawRequest {
    param(
        [string]$Url,
        [string]$Method = 'GET',
        [hashtable]$Headers = @{},
        [string]$JsonBody,
        [switch]$Anonymous
    )

    $bodyFile = [System.IO.Path]::GetTempFileName()
    try {
        $curlArgs = @('-k', '-s', '-S', '--max-time', '60', '-X', $Method, '-o', $bodyFile, '-w', '%{http_code}|%{content_type}')

        # -u ":" hands curl the caller's own Windows credentials. Anonymous is for the checks that
        # assert an endpoint REJECTS an unauthenticated caller — those must not send any.
        if (-not $Anonymous) {
            $curlArgs += @('--negotiate', '-u', ':')
        }

        foreach ($key in $Headers.Keys) {
            $curlArgs += @('-H', ("{0}: {1}" -f $key, $Headers[$key]))
        }

        if ($null -ne $JsonBody) {
            $curlArgs += @('-H', 'Content-Type: application/json', '--data-binary', $JsonBody)
        }

        $curlArgs += $Url

        $written = & 'curl.exe' @curlArgs 2>&1
        $marker = ($written | Select-Object -Last 1) -as [string]

        $status = 0
        $contentType = ''
        if ($marker -and $marker.Contains('|')) {
            $parts = $marker.Split('|')
            $status = [int]($parts[0])
            $contentType = $parts[1]
        }

        $body = ''
        if (Test-Path $bodyFile) {
            $body = Get-Content -Path $bodyFile -Raw -ErrorAction SilentlyContinue
            if ($null -eq $body) { $body = '' }
        }

        return [pscustomobject]@{
            Status      = $status
            ContentType = $contentType
            Body        = $body
        }
    }
    finally {
        Remove-Item -Path $bodyFile -Force -ErrorAction SilentlyContinue
    }
}

<#
    Walks a dotted path such as "data[0].code" over parsed JSON. Property access has to go through
    PSObject.Properties: under Set-StrictMode, reading a property that does not exist throws, and
    a missing property is exactly the condition this function is here to report.
#>
function Resolve-JsonPath {
    param($Node, [string]$Path)

    $current = $Node
    foreach ($segment in ($Path -split '\.')) {
        if ($null -eq $current) {
            return [pscustomobject]@{ Found = $false; Value = $null }
        }

        $name = $segment
        $indexes = @()
        while ($name -match '^(.*)\[(\d+)\]$') {
            $indexes = @([int]$Matches[2]) + $indexes
            $name = $Matches[1]
        }

        if ($name -ne '') {
            $property = $current.PSObject.Properties[$name]
            if ($null -eq $property) {
                return [pscustomobject]@{ Found = $false; Value = $null }
            }
            $current = $property.Value
        }

        foreach ($index in $indexes) {
            $asArray = @($current)
            if ($index -ge $asArray.Count) {
                return [pscustomobject]@{ Found = $false; Value = $null }
            }
            $current = $asArray[$index]
        }
    }

    return [pscustomobject]@{ Found = $true; Value = $current }
}

function Add-Result {
    param(
        [string]$Label,
        [string]$Outcome,
        [string]$Detail,
        [string]$Url
    )

    [void]$script:Results.Add([pscustomobject]@{
        Label   = $Label
        Outcome = $Outcome
        Detail  = $Detail
        Url     = $Url
    })

    $colour = 'Green'
    if ($Outcome -eq 'FAIL') { $colour = 'Red' }
    elseif ($Outcome -eq 'SKIP') { $colour = 'Yellow' }

    $line = "[{0}] {1}" -f $Outcome, $Label
    if ($Detail) { $line += " — $Detail" }
    Write-Host $line -ForegroundColor $colour
}

<#
    One assertion per endpoint.

    ExpectStatus is a list because the correct answer is sometimes environment-dependent: a detail
    endpoint on an empty database legitimately answers 404. It is never a blanket "anything that
    responds", and 401 has to be asked for explicitly — see the ApiKey check below, where 401 is
    the whole point.
#>
function Invoke-SmokeCheck {
    param(
        [string]$Label,
        [string]$Url,
        [int[]]$ExpectStatus = @(200),
        [string[]]$ExpectJsonPath = @(),
        [string]$ExpectContentType = 'application/json',
        [string]$Method = 'GET',
        [string]$JsonBody,
        [hashtable]$Headers = @{},
        [switch]$Anonymous,
        [switch]$PassThru
    )

    $response = $null
    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        $response = Invoke-RawRequest -Url $Url -Method $Method -Headers $Headers -JsonBody $JsonBody -Anonymous:$Anonymous

        # Only retry the codes that a warming app pool actually produces. Retrying a 500 just
        # takes three times as long to report the same failure.
        if ($response.Status -ne 0 -and $response.Status -ne 502 -and $response.Status -ne 503 -and $response.Status -ne 504) {
            break
        }
        if ($attempt -lt $MaxAttempts) {
            Start-Sleep -Seconds 2
        }
    }

    if ($Detailed -and $response.Body) {
        $preview = $response.Body
        if ($preview.Length -gt 400) { $preview = $preview.Substring(0, 400) + '...' }
        Write-Host "    body: $preview" -ForegroundColor DarkGray
    }

    if ($ExpectStatus -notcontains $response.Status) {
        $detail = "expected {0}, got {1}" -f ($ExpectStatus -join '/'), $response.Status
        if ($response.Status -eq 401) {
            $detail += ' (authentication failed — this is NOT a pass)'
        }
        # The body of a failure is the most useful thing on the screen, so show it even without
        # -Detailed. This is where the "Invalid column name 'SourceSystem'" would have appeared.
        if ($response.Body) {
            $snippet = ($response.Body -replace '\s+', ' ')
            if ($snippet.Length -gt 300) { $snippet = $snippet.Substring(0, 300) + '...' }
            $detail += " | $snippet"
        }
        Add-Result -Label $Label -Outcome 'FAIL' -Detail $detail -Url $Url
        return $null
    }

    # Everything past here only makes sense for a success; an expected 401/404 has no payload to
    # check and asserting one would make the check fail for the wrong reason.
    if ($response.Status -ge 400) {
        Add-Result -Label $Label -Outcome 'PASS' -Detail ("{0} as expected" -f $response.Status) -Url $Url
        return $null
    }

    if ($ExpectContentType -and $response.ContentType -notlike "$ExpectContentType*") {
        Add-Result -Label $Label -Outcome 'FAIL' -Detail ("content type '{0}', expected '{1}*' — an IIS error page returns 200" -f $response.ContentType, $ExpectContentType) -Url $Url
        return $null
    }

    $parsed = $null
    if ($ExpectContentType -like 'application/json*') {
        try {
            $parsed = $response.Body | ConvertFrom-Json
        }
        catch {
            Add-Result -Label $Label -Outcome 'FAIL' -Detail 'body is not valid JSON' -Url $Url
            return $null
        }
    }

    foreach ($path in $ExpectJsonPath) {
        $resolved = Resolve-JsonPath -Node $parsed -Path $path
        if (-not $resolved.Found) {
            Add-Result -Label $Label -Outcome 'FAIL' -Detail ("response is missing '{0}' — the projection no longer matches what callers read" -f $path) -Url $Url
            return $null
        }
    }

    $summary = "200"
    if ($ExpectJsonPath.Count -gt 0) {
        $summary += ", {0} field(s) verified" -f $ExpectJsonPath.Count
    }
    Add-Result -Label $Label -Outcome 'PASS' -Detail $summary -Url $Url

    if ($PassThru) { return $parsed }
    return $null
}

Write-Host "QCS API smoke tests" -ForegroundColor White
Write-Host "Target: $base" -ForegroundColor White
Write-Host ("Caller: {0}\{1}" -f $env:USERDOMAIN, $env:USERNAME) -ForegroundColor DarkGray

# ---------------------------------------------------------------------------------------------
Write-Step 'Session and authentication'

# First, and on its own: if Negotiate is broken every other check fails in a way that looks like
# an application bug. This one names the real cause. It also covers the deploy failing to replace
# Microsoft.AspNetCore.Authentication.Negotiate.dll, which happened on 2026-08-06 (ERROR 32).
Invoke-SmokeCheck -Label 'Session/Me (Windows auth)' `
    -Url "$base/api/Session/Me" `
    -ExpectJsonPath @('nId')

# ---------------------------------------------------------------------------------------------
Write-Step 'Dashboard'

Invoke-SmokeCheck -Label 'Dashboard summary'   -Url "$base/api/Dashboard/Summary"
Invoke-SmokeCheck -Label 'Requester trend'     -Url "$base/api/Dashboard/RequesterTrend?days=7&top=5"
Invoke-SmokeCheck -Label 'Validity status'     -Url "$base/api/Dashboard/ValidityStatus"
Invoke-SmokeCheck -Label 'Active vendors'      -Url "$base/api/Dashboard/ActiveVendors?top=10"
Invoke-SmokeCheck -Label 'Trend window'        -Url "$base/api/Dashboard/RequestTrend?timeframe=7d&aggregation=day"

# ---------------------------------------------------------------------------------------------
Write-Step 'Admin lists (RequestGridDto)'

$adminList = Invoke-SmokeCheck -Label 'Admin all requests' `
    -Url "$base/api/Request/Admin/All?skip=0&take=1&requireTotalCount=true" `
    -ExpectJsonPath @('totalCount') `
    -PassThru

Invoke-SmokeCheck -Label 'Admin requesters' `
    -Url "$base/api/Request/Admin/Requesters?skip=0&take=5&sort=%5B%7B%22selector%22%3A%22quotationCount%22%2C%22desc%22%3Atrue%7D%5D"

# ---------------------------------------------------------------------------------------------
Write-Step 'Portal lists (PortalRequestListItemDto)'

$portalList = Invoke-SmokeCheck -Label 'Portal requests' `
    -Url "$base/api/Portal/Requests?view=MyRequests&page=1&pageSize=50" `
    -ExpectJsonPath @('items', 'totalCount', 'page') `
    -PassThru

# The other views run different queries, so a list check on one view does not cover the rest.
# Names come from PortalRequestsController's own validation message — MyTasks, MyRequests,
# MyApproved, Rejected, AllApproved. Anything not on that list is a 400, not an empty page.
Invoke-SmokeCheck -Label 'Portal requests (MyTasks view)' `
    -Url "$base/api/Portal/Requests?view=MyTasks&page=1&pageSize=1" `
    -ExpectJsonPath @('items', 'totalCount')

Invoke-SmokeCheck -Label 'Portal requests (AllApproved view)' `
    -Url "$base/api/Portal/Requests?view=AllApproved&page=1&pageSize=1" `
    -ExpectJsonPath @('items', 'totalCount')

# A wrong view name must be rejected, not quietly treated as a default. Cheap guard against the
# validation being dropped in a refactor.
Invoke-SmokeCheck -Label 'Portal requests rejects an unknown view' `
    -Url "$base/api/Portal/Requests?view=NotAView&page=1&pageSize=1" `
    -ExpectStatus @(400)

if ($null -ne $portalList) {
    $unknownStatuses = @($portalList.items | Where-Object { $_.status -notin @(0, 1, 2, 3, 4, 5, 6) })
    if ($unknownStatuses.Count -gt 0) {
        Add-Result -Label 'Portal status contract uses only central values' -Outcome 'FAIL' `
            -Detail ("unknown status value(s): {0}" -f (($unknownStatuses | ForEach-Object { $_.status } | Sort-Object -Unique) -join ', ')) `
            -Url "$base/api/Portal/Requests?view=MyRequests"
    }
    else {
        Add-Result -Label 'Portal status contract uses only central values' -Outcome 'PASS' `
            -Detail 'all sampled statuses are in 0..6' `
            -Url "$base/api/Portal/Requests?view=MyRequests"
    }
}

$previewBody = @{
    title = 'QCS smoke route preview'
    vendorCode = 'SMOKE'
    vendorName = 'Smoke Test'
} | ConvertTo-Json -Compress

Invoke-SmokeCheck -Label 'Central approval route preview' `
    -Url "$base/api/Portal/Requests/route-preview" `
    -Method 'POST' `
    -JsonBody $previewBody `
    -ExpectJsonPath @('steps')

$missingRequestId = 2147483647
$actionBody = @{ comment = 'Route existence smoke check' } | ConvertTo-Json -Compress
Invoke-SmokeCheck -Label 'Return route exists without mutating data' `
    -Url "$base/api/Portal/Requests/$missingRequestId/return" `
    -Method 'POST' `
    -JsonBody $actionBody `
    -ExpectStatus @(404)

Invoke-SmokeCheck -Label 'Cancel route exists without mutating data' `
    -Url "$base/api/Portal/Requests/$missingRequestId/cancel" `
    -Method 'POST' `
    -JsonBody $actionBody `
    -ExpectStatus @(404)

# ---------------------------------------------------------------------------------------------
# The section that would have caught the 2026-08-06 outage. Everything below reads a request
# DETAIL, which is the DTO family that releases keep changing and that nothing above touches.
Write-Step 'Request detail (RequestDetailDto) — chained from a live row'

$sampleId = $null
$sampleCode = $null
if ($null -ne $adminList) {
    $idPath = Resolve-JsonPath -Node $adminList -Path 'data[0].id'
    $codePath = Resolve-JsonPath -Node $adminList -Path 'data[0].code'
    if ($idPath.Found) { $sampleId = $idPath.Value }
    if ($codePath.Found) { $sampleCode = $codePath.Value }
}

if ($null -eq $sampleId -or $null -eq $sampleCode) {
    # Reported, never silently passed: a suite that goes quiet when it has no data is how a gap
    # like this one survives a release.
    Add-Result -Label 'Request detail checks' -Outcome 'SKIP' -Detail 'no request rows available to sample' -Url "$base/api/Request/Admin/All"
}
else {
    Write-Host "    sampling id=$sampleId code=$sampleCode" -ForegroundColor DarkGray

    # Only fields that are ALWAYS present may be asserted here.
    #
    # The API sets DefaultIgnoreCondition = WhenWritingNull
    # (QCS.API/Extensions/ApiServiceCollectionExtensions.cs:32), so a null property is omitted from
    # the response entirely rather than sent as null. A nullable field therefore cannot be checked
    # by presence: sourceSystem is absent for every request that did not come from QRS, which is
    # most of them, and asserting it would fail on healthy data. Value types and the initialised
    # Permissions object are the reliable ones.
    #
    # That does not leave the 2026-08-06 class of bug undetected. A projection selecting a column
    # the database does not have fails in SQL translation and comes back 500 — so it is the status
    # assertion that catches it, and it is the mere existence of these four checks that matters.
    # Before today nothing in the suite read a request detail at all.
    $detailFields = @('requestId', 'code', 'requestDate', 'currentStepId', 'permissions')

    Invoke-SmokeCheck -Label 'Request detail by id' `
        -Url "$base/api/Request/Detail/$sampleId" `
        -ExpectJsonPath $detailFields

    Invoke-SmokeCheck -Label 'Request detail by code (route)' `
        -Url ("{0}/api/Request/ByCode/{1}" -f $base, [uri]::EscapeDataString($sampleCode)) `
        -ExpectJsonPath $detailFields

    Invoke-SmokeCheck -Label 'Request detail by code (query)' `
        -Url ("{0}/api/Request/ByCode?code={1}" -f $base, [uri]::EscapeDataString($sampleCode)) `
        -ExpectJsonPath $detailFields

    # The endpoint the user actually hit the 500 on.
    Invoke-SmokeCheck -Label 'Quotation by code' `
        -Url ("{0}/api/Quotation/ByCode?code={1}" -f $base, [uri]::EscapeDataString($sampleCode)) `
        -ExpectJsonPath $detailFields
}

if ([string]::IsNullOrWhiteSpace($SourceLinkedCode)) {
    Add-Result -Label 'QRS source link is populated' -Outcome 'SKIP' -Detail 'pass -SourceLinkedCode <code> to verify sourceSystem/sourceCode' -Url ''
}
else {
    Invoke-SmokeCheck -Label 'QRS source link is populated' `
        -Url ("{0}/api/Quotation/ByCode?code={1}" -f $base, [uri]::EscapeDataString($SourceLinkedCode)) `
        -ExpectJsonPath @('requestId', 'code', 'sourceSystem', 'sourceCode')
}

# ---------------------------------------------------------------------------------------------
Write-Step 'Portal detail (PortalRequestDetailDto)'

$portalId = $null
$portalCode = $null
if ($null -ne $portalList) {
    $portalIdPath = Resolve-JsonPath -Node $portalList -Path 'items[0].id'
    $portalCodePath = Resolve-JsonPath -Node $portalList -Path 'items[0].code'
    if ($portalIdPath.Found) { $portalId = $portalIdPath.Value }
    if ($portalCodePath.Found) { $portalCode = $portalCodePath.Value }
}

if ($null -eq $portalId) {
    Add-Result -Label 'Portal detail checks' -Outcome 'SKIP' -Detail 'caller has no visible portal requests' -Url "$base/api/Portal/Requests"
}
else {
    $portalFields = @('id', 'code', 'title', 'statusName', 'permissions', 'documents', 'workflowSteps', 'histories')

    Invoke-SmokeCheck -Label 'Portal detail by id' `
        -Url "$base/api/Portal/Requests/$portalId" `
        -ExpectJsonPath $portalFields

    if ($null -ne $portalCode) {
        Invoke-SmokeCheck -Label 'Portal detail by code' `
            -Url ("{0}/api/Portal/Requests/by-code/{1}" -f $base, [uri]::EscapeDataString($portalCode)) `
            -ExpectJsonPath $portalFields
    }
}

# ---------------------------------------------------------------------------------------------
Write-Step 'Integration surface and its auth gate'

Invoke-SmokeCheck -Label 'Integration GetRequestAll (domain user)' `
    -Url "$base/api/Integration/GetRequestAll"

# 401 is the pass here, and this is why a blanket "401 means responsive" rule is dangerous: it
# would have marked this endpoint healthy whether the API key gate was working or wide open.
# Anonymous, because sending the caller's Windows credentials would not exercise the gate.
Invoke-SmokeCheck -Label 'GetRequestsBySource rejects callers without an API key' `
    -Url "$base/api/Integration/GetRequestsBySource?system=QRS" `
    -ExpectStatus @(401) `
    -Anonymous

Invoke-SmokeCheck -Label 'QrsSourcing requests' `
    -Url "$base/api/QrsSourcing/Requests"

# ---------------------------------------------------------------------------------------------
Write-Step 'Unauthenticated access is refused'

# Straightforward regression guard: if a deploy ever drops the auth wiring, the endpoints above
# would all still pass — they authenticate fine. Only an anonymous call notices.
#
# Know what this check CANNOT tell you: IIS answers 401 before routing, so it returns 401 for a
# path that does not exist just as readily as for one that does. Verified by pointing the suite at
# a bogus base URL — this check and the API-key one still passed while the other fourteen failed.
# They prove the gate rejects, never that the app is deployed. The authenticated checks above are
# what prove that, which is the whole reason they may not treat 401 as success.
Invoke-SmokeCheck -Label 'Session/Me refuses anonymous callers' `
    -Url "$base/api/Session/Me" `
    -ExpectStatus @(401) `
    -Anonymous

# ---------------------------------------------------------------------------------------------
Write-Step 'Summary'

$failed = @($script:Results | Where-Object { $_.Outcome -eq 'FAIL' })
$skipped = @($script:Results | Where-Object { $_.Outcome -eq 'SKIP' })
$passed = @($script:Results | Where-Object { $_.Outcome -eq 'PASS' })

Write-Host ("{0} passed, {1} failed, {2} skipped, {3} total" -f $passed.Count, $failed.Count, $skipped.Count, $script:Results.Count) -ForegroundColor White

if ($skipped.Count -gt 0) {
    Write-Host "`nSkipped (not verified — do not read these as passes):" -ForegroundColor Yellow
    foreach ($result in $skipped) {
        Write-Host ("  - {0}: {1}" -f $result.Label, $result.Detail) -ForegroundColor Yellow
    }
}

if ($failed.Count -gt 0) {
    Write-Host "`nFailed:" -ForegroundColor Red
    foreach ($result in $failed) {
        Write-Host ("  - {0}" -f $result.Label) -ForegroundColor Red
        Write-Host ("    {0}" -f $result.Detail) -ForegroundColor Red
        Write-Host ("    {0}" -f $result.Url) -ForegroundColor DarkGray
    }
    throw ("{0} smoke check(s) failed against {1}" -f $failed.Count, $base)
}

Write-Host "`nAll smoke checks passed." -ForegroundColor Green
