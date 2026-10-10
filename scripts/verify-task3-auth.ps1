$ErrorActionPreference = "Stop"
$base = "http://localhost:5043/api"
$body = @{ email = "alex.turner@northwind-support.example"; password = "LocalOnly_Dev_Pass_2026!" } | ConvertTo-Json

function Check-401([string]$url, [string]$method = 'GET', [string]$json = '') {
    try {
        $params = @{ Uri = $url; Method = $method; UseBasicParsing = $true }
        if ($json) { $params.ContentType = 'application/json'; $params.Body = $json }
        Invoke-WebRequest @params | Out-Null
        throw "Expected 401 at $url"
    } catch [System.Net.WebException] {
        if ([int]$_.Exception.Response.StatusCode -ne 401) { throw }
    }
}

Check-401 "$base/tickets"
Write-Host "Unauthenticated tickets request: 401 PASS"

$wrong = @{ email = 'alex.turner@northwind-support.example'; password = 'WrongPassword' } | ConvertTo-Json
Check-401 "$base/auth/login" 'POST' $wrong
Write-Host "Wrong-password login: 401 PASS"

$inactive = @{ email = 'yuki.tanaka@northwind-support.example'; password = 'LocalOnly_Dev_Pass_2026!' } | ConvertTo-Json
Check-401 "$base/auth/login" 'POST' $inactive
Write-Host "Inactive-agent login: 401 PASS"

$login = Invoke-RestMethod -Uri "$base/auth/login" -Method Post -ContentType 'application/json' -Body $body
if (-not $login.token -or $login.user.fullName -ne 'Alex Turner') { throw 'Login failed' }
$headers = @{ Authorization = "Bearer $($login.token)" }
$me = Invoke-RestMethod -Uri "$base/auth/me" -Headers $headers
if ($me.email -ne 'alex.turner@northwind-support.example') { throw 'GET /auth/me failed' }
$list = Invoke-RestMethod -Uri "$base/tickets?page=1&pageSize=1" -Headers $headers
if ($null -eq $list.totalCount) { throw 'Protected tickets request failed' }
Write-Host "Login, /me and authorized /tickets: PASS"

# Verify actor is NOT sourced from the request body.
$newTicketBody = @{
    title='Auth escalation actor verification'
    description='Confirm escalation actor is taken from signed-in agent JWT'
    customerId=2; categoryId=3; requestedPriority='Low'
} | ConvertTo-Json
$t = Invoke-RestMethod -Uri "$base/tickets" -Headers $headers -Method Post -ContentType 'application/json' -Body $newTicketBody
$escalateBody = @{ reason='Urgent agent authentication verification'; escalatedBy='SPOOFED-USER' } | ConvertTo-Json
$escalation = Invoke-RestMethod -Uri "$base/tickets/$($t.id)/escalate" -Headers $headers -Method Post -ContentType 'application/json' -Body $escalateBody
if ($escalation.escalation.escalatedBy -ne $me.email) { throw 'Escalation actor must come from JWT' }
Write-Host "Escalation actor from JWT: PASS"
Write-Host "TASK 3 LIVE AUTH VERIFICATION: PASS"
