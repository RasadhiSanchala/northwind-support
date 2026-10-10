# Authenticated Task 2-D read-only smoke test for the Task 3-protected API.
# Requires the Development API/SQL Server at http://localhost:5043.
# Uses only GETs after obtaining a JWT; creates/updates no tickets.
$ErrorActionPreference = 'Stop'
$base = if ($env:SUPPORTDESK_API_BASE) { $env:SUPPORTDESK_API_BASE.TrimEnd('/') } else { 'http://localhost:5043/api' }
$email = if ($env:SUPPORTDESK_DEV_EMAIL) { $env:SUPPORTDESK_DEV_EMAIL } else { 'alex.turner@northwind-support.example' }
$password = if ($env:SUPPORTDESK_DEV_PASSWORD) { $env:SUPPORTDESK_DEV_PASSWORD } else { 'LocalOnly_Dev_Pass_2026!' }

$loginBody = @{ email = $email; password = $password } | ConvertTo-Json
$login = Invoke-RestMethod -Uri "$base/auth/login" -Method Post -ContentType 'application/json' -Body $loginBody
if (-not $login.token) { throw 'Authentication returned no bearer token.' }
$headers = @{ Authorization = "Bearer $($login.token)" }

$states = @('NotApplicable', 'WithinSla', 'AtRisk', 'Breached', 'Met')
$sum = 0
foreach ($state in $states) {
    $pageNumber = 1
    $seen = 0
    do {
        $uri = "$base/tickets?slaStatus=$state&page=$pageNumber&pageSize=100"
        $result = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get
        foreach ($ticket in $result.items) {
            if ($ticket.slaStatus -ne $state) {
                throw "$state returned $($ticket.reference) with SLA status $($ticket.slaStatus)"
            }
            $seen++
        }
        $pageNumber++
    } while ($pageNumber -le $result.totalPages)

    if ($seen -ne $result.totalCount) {
        throw "Filtered count mismatch for $state`: $seen vs $($result.totalCount)"
    }
    Write-Host "$state`: $seen tickets - PASS"
    $sum += $seen
}
$all = Invoke-RestMethod -Uri "$base/tickets?page=1&pageSize=1" -Headers $headers -Method Get
if ($sum -ne $all.totalCount) {
    throw "Overall partition mismatch: $sum vs $($all.totalCount)"
}
Write-Host "T2-D AUTHENTICATED SQL FILTER/PAGING: PASS ($sum tickets)"
