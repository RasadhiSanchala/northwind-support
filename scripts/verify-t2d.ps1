# Requires a running development API at http://localhost:5043 and migrated SQL Server.
# Non-destructive: only performs GET requests; validates SQL filtering/count/paging.
$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5043/api/tickets'
$states = @('NotApplicable', 'WithinSla', 'AtRisk', 'Breached', 'Met')
$grandTotal = 0

foreach ($state in $states) {
    $pageNumber = 1
    $seen = 0
    do {
        $uri = "${base}?slaStatus=$state&page=$pageNumber&pageSize=100"
        $response = Invoke-RestMethod -Uri $uri -Method Get
        foreach ($ticket in $response.items) {
            if ($ticket.slaStatus -ne $state) {
                throw "$state filter returned $($ticket.reference) with status $($ticket.slaStatus)"
            }
            $seen++
        }
        $pageNumber++
    } while ($pageNumber -le $response.totalPages)

    if ($seen -ne $response.totalCount) {
        throw "Count mismatch for $state - expected $($response.totalCount), saw $seen"
    }
    Write-Host "$state : $seen ticket(s) - PASS"
    $grandTotal += $seen
}

$unfiltered = Invoke-RestMethod -Uri "${base}?page=1&pageSize=1" -Method Get
if ($grandTotal -ne $unfiltered.totalCount) {
    throw "SLA partition count mismatch - $grandTotal vs $($unfiltered.totalCount)"
}
Write-Host "T2-D SQL FILTER AND PAGING: PASS ($grandTotal tickets across all five states)"
