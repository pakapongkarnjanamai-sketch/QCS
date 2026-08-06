<#
.SYNOPSIS
    Deletes ALL request business data from the QCS QA database. Destructive and irreversible.

.DESCRIPTION
    PLAN-051 Phase 7. The central Approval migration changes what a request's status and current
    step mean, and QA's rows were written by the retired local engine. The user's explicit decision
    was to reset QA rather than replay its approval history, because fabricating migrated approval
    timestamps produces a record that reads as real and is not.

    What this deletes, in foreign-key order inside one transaction:

        ApprovalSteps   -> child of Requests
        Quotations      -> child of Requests, and holds the FK to AttachmentFiles
        AttachmentFiles -> unreferenced once Quotations are gone
        Requests

    What it deliberately KEEPS: AdminUserAccesses, Roles, UserRoles, Users, Departments,
    UserDepartments. Access and configuration are not business data, and rebuilding them by hand
    after a reset is how a QA environment quietly stops matching PROD.

    Safety, in the order the checks run:

      1. Every target is a mandatory parameter. There is no default and no fallback, because a
         default target is what caused the PROD incident on 2026-08-06.
      2. The SQL instance is matched against an explicit QA allowlist BEFORE any connection is
         opened. A host that is merely "not obviously PROD" is rejected.
      3. -IAcceptDataLoss must be passed. A confirmation prompt is not enough: this script has to
         be safe when run non-interactively, where a prompt is either skipped or auto-answered.

    No credentials in source. Windows authentication is used unless -Credential is supplied.

.PARAMETER SqlInstance
    QA SQL host. Must be on the allowlist below.

.PARAMETER Database
    Database name. Must be QCS.

.PARAMETER IAcceptDataLoss
    Required. Without it the script reports what it would delete and exits without changing data.

.PARAMETER Credential
    Optional SQL login. Omit to use Windows authentication.

.EXAMPLE
    .\Reset-QCS-QA-BusinessData.ps1 -SqlInstance 10.10.143.37 -Database QCS
    Dry run: prints row counts and exits.

.EXAMPLE
    .\Reset-QCS-QA-BusinessData.ps1 -SqlInstance 10.10.143.37 -Database QCS -IAcceptDataLoss
    Deletes.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$SqlInstance,

    [Parameter(Mandatory)]
    [ValidateSet('QCS')]
    [string]$Database,

    [switch]$IAcceptDataLoss,

    [System.Management.Automation.PSCredential]$Credential
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# The QA database, and nothing else. Adding a host here is a deliberate act that shows up in a
# diff and in review — which is the point.
$AllowedInstances = @('10.10.143.37')

# Named explicitly so a mistyped or copy-pasted PROD target fails with a message that says what
# happened, rather than falling through the allowlist with a generic "not allowed".
$KnownProductionHosts = @('10.10.154.21', 'ap-ntc2137-prwb', 'ap-ntc2139-coss')

function Write-Step {
    param([string]$Message)
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

# ── Validation: everything below runs BEFORE a connection is opened ─────────────────────────────
Write-Step 'Validating target'

$normalized = $SqlInstance.Trim().ToLowerInvariant()

if ($KnownProductionHosts -contains $normalized) {
    throw "REFUSED: '$SqlInstance' is a production host. This script resets QA data only, and PROD data migration is a separate human-only plan."
}

if ($AllowedInstances -notcontains $normalized) {
    throw "REFUSED: '$SqlInstance' is not on the QA allowlist ($($AllowedInstances -join ', ')). Add it deliberately if QA has genuinely moved; do not pass an unknown host."
}

Write-Host "  Instance : $SqlInstance (allowlisted QA)" -ForegroundColor Green
Write-Host "  Database : $Database" -ForegroundColor Green
Write-Host "  Mode     : $(if ($IAcceptDataLoss) { 'DELETE' } else { 'dry run — no data will change' })" -ForegroundColor $(if ($IAcceptDataLoss) { 'Yellow' } else { 'Green' })

# ── Connection ─────────────────────────────────────────────────────────────────────────────────
$builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
$builder['Data Source'] = $SqlInstance
$builder['Initial Catalog'] = $Database
$builder['TrustServerCertificate'] = $true
$builder['Connect Timeout'] = 15
if ($null -ne $Credential) {
    $builder['User ID'] = $Credential.UserName
    $builder['Password'] = $Credential.GetNetworkCredential().Password
} else {
    $builder['Integrated Security'] = $true
}

$connection = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)

# Child tables first; Requests last. AttachmentFiles sit after Quotations because the FK lives on
# Quotations, so they only become deletable once those rows are gone.
$tables = @('ApprovalSteps', 'Quotations', 'AttachmentFiles', 'Requests')
$preserved = @('AdminUserAccesses', 'Roles', 'UserRoles', 'Users', 'Departments', 'UserDepartments')

function Get-Counts {
    param($Connection, $Transaction)
    $counts = [ordered]@{}
    foreach ($table in ($tables + $preserved)) {
        $command = $Connection.CreateCommand()
        if ($null -ne $Transaction) { $command.Transaction = $Transaction }
        $command.CommandText = "SELECT COUNT(*) FROM [dbo].[$table]"
        try { $counts[$table] = [int]$command.ExecuteScalar() } catch { $counts[$table] = 'n/a' }
    }
    return $counts
}

try {
    $connection.Open()
    Write-Host "  Connected to $($connection.DataSource) / $($connection.Database)" -ForegroundColor DarkGray

    # Confirms the server actually reached is the one asked for. A DNS alias or a saved alias in
    # SQL client config can quietly land a connection somewhere else.
    $check = $connection.CreateCommand()
    $check.CommandText = 'SELECT DB_NAME()'
    $actualDatabase = [string]$check.ExecuteScalar()
    if ($actualDatabase -ne $Database) {
        throw "REFUSED: connected to database '$actualDatabase', expected '$Database'."
    }

    Write-Step 'Row counts before'
    $before = Get-Counts -Connection $connection -Transaction $null
    $before.GetEnumerator() | ForEach-Object {
        $marker = if ($tables -contains $_.Key) { 'DELETE' } else { 'keep  ' }
        Write-Host ("  [{0}] {1,-18} {2}" -f $marker, $_.Key, $_.Value)
    }

    if (-not $IAcceptDataLoss) {
        Write-Host "`nDry run complete. Nothing was changed." -ForegroundColor Green
        Write-Host "Re-run with -IAcceptDataLoss to delete the rows marked DELETE above." -ForegroundColor Yellow
        return
    }

    Write-Step 'Deleting business data (single transaction)'
    $transaction = $connection.BeginTransaction()
    try {
        # Quotations gained a self-referencing FK in 20260806071626_AddExpiredQuotationReference
        # (SourceQuotationId, DeleteBehavior.Restrict) so an expired quotation can be reused by a
        # later request. A single DELETE over the whole table should satisfy the constraint, but
        # "should" is not what a destructive script runs on: breaking the self-reference first makes
        # the delete order-independent and costs one statement.
        $breakSelfReference = $connection.CreateCommand()
        $breakSelfReference.Transaction = $transaction
        $breakSelfReference.CommandText = @'
IF COL_LENGTH('dbo.Quotations', 'SourceQuotationId') IS NOT NULL
    UPDATE [dbo].[Quotations] SET [SourceQuotationId] = NULL WHERE [SourceQuotationId] IS NOT NULL;
'@
        $unlinked = $breakSelfReference.ExecuteNonQuery()
        if ($unlinked -gt 0) {
            Write-Host ("  {0,-18} {1} self-references cleared first" -f 'Quotations', $unlinked) -ForegroundColor DarkGray
        }

        foreach ($table in $tables) {
            $delete = $connection.CreateCommand()
            $delete.Transaction = $transaction
            # DELETE, not TRUNCATE: TRUNCATE cannot run against a table with an inbound foreign key
            # and would force dropping constraints, which is a far worse thing to get wrong.
            $delete.CommandText = "DELETE FROM [dbo].[$table]"
            $removed = $delete.ExecuteNonQuery()
            Write-Host ("  {0,-18} {1} rows deleted" -f $table, $removed) -ForegroundColor Yellow
        }

        # Identity reseed so the reset environment starts from 1 rather than continuing from
        # whatever the old data reached. Guarded: RESEED throws if the table never had an identity.
        foreach ($table in $tables) {
            $reseed = $connection.CreateCommand()
            $reseed.Transaction = $transaction
            $reseed.CommandText = @"
IF OBJECTPROPERTY(OBJECT_ID(N'dbo.$table'), 'TableHasIdentity') = 1
    DBCC CHECKIDENT (N'dbo.$table', RESEED, 0) WITH NO_INFOMSGS;
"@
            [void]$reseed.ExecuteNonQuery()
        }

        $transaction.Commit()
        Write-Host "  Committed." -ForegroundColor Green
    }
    catch {
        $transaction.Rollback()
        Write-Host "  Rolled back — no rows were deleted." -ForegroundColor Red
        throw
    }

    Write-Step 'Row counts after'
    $after = Get-Counts -Connection $connection -Transaction $null
    $after.GetEnumerator() | ForEach-Object {
        Write-Host ("  {0,-18} {1}" -f $_.Key, $_.Value)
    }

    # The preserved tables are checked, not assumed. "I kept the access data" is worth exactly as
    # much as the count that proves it.
    $lost = $preserved | Where-Object { $before[$_] -is [int] -and $after[$_] -is [int] -and $after[$_] -ne $before[$_] }
    if ($lost) {
        throw "Preserved tables changed, which should be impossible: $($lost -join ', '). Investigate before using this environment."
    }

    Write-Host "`nQA business data reset complete. Access and configuration data untouched." -ForegroundColor Green
    Write-Host "Next: apply the EF migration, then deploy per PLAN-051 Phase 7 — QRS first." -ForegroundColor Green
}
finally {
    if ($connection.State -ne [System.Data.ConnectionState]::Closed) { $connection.Close() }
}
