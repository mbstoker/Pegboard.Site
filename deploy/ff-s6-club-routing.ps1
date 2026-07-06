<#
.SYNOPSIS
    FF-S6 — asserts the www/club/{slug} routing + static-SSR contract (seam S6, ADR-0010).
    Run against a live marketing host that has the S6 reverse-proxy configured.

.EXAMPLE
    ./ff-s6-club-routing.ps1 -BaseUrl https://www.staging.epegboard.com -Slug club-mk -SessionId 123

.NOTES
    Assertions 3-5 (Site fall-through + boundary) pass with the proxy alone.
    Assertions 1-2 additionally require #632 (static-SSR /club/*) on the fronted app env.
#>
param(
    [Parameter(Mandatory)][string]$BaseUrl,
    [Parameter(Mandatory)][string]$Slug,
    [string]$SessionId,
    # A substring that MUST appear in a Site-rendered page (proves Site served it).
    [string]$SiteMarker = '/features',
    # A substring that MUST appear in the app's club page (proves the app served it).
    [string]$ClubMarker = ''   # e.g. the club name; if empty we only assert 200 + html + no-circuit
)
$ErrorActionPreference = 'Stop'
$fails = 0
function Get-Page($url) {
    try {
        $r = Invoke-WebRequest -UseBasicParsing -Uri $url -MaximumRedirection 0 -TimeoutSec 20
        return @{ Status = [int]$r.StatusCode; Body = $r.Content; CT = $r.Headers['Content-Type'] }
    } catch {
        $resp = $_.Exception.Response
        if ($resp) { return @{ Status = [int]$resp.StatusCode; Body = ''; CT = '' } }
        return @{ Status = -1; Body = $_.Exception.Message; CT = '' }
    }
}
function Assert($name, $cond, $detail) {
    if ($cond) { Write-Output "PASS  $name" }
    else { Write-Output "FAIL  $name -- $detail"; $script:fails++ }
}

$baseTrim = $BaseUrl.TrimEnd('/')

# 1. /club/{slug} -> 200 static HTML, no interactive-circuit dependency
$club = Get-Page "$baseTrim/club/$Slug"
$noCircuit = ($club.Body -notmatch 'blazor\.server\.js') -and ($club.Body -notmatch '_blazor')
Assert 'A1 /club/{slug} -> 200'          ($club.Status -eq 200)               "status=$($club.Status)"
Assert 'A1 /club/{slug} is text/html'    ($club.CT -match 'text/html')        "ct=$($club.CT)"
Assert 'A1 /club/{slug} is static (no interactive circuit)' $noCircuit        'found blazor.server.js/_blazor reference'
if ($ClubMarker) { Assert 'A1 /club/{slug} contains club marker' ($club.Body -match [regex]::Escape($ClubMarker)) "marker '$ClubMarker' absent" }

# 2. /club/{slug}/session/{id} (needs #632)
if ($SessionId) {
    $rec = Get-Page "$baseTrim/club/$Slug/session/$SessionId"
    $recNoCircuit = ($rec.Body -notmatch 'blazor\.server\.js') -and ($rec.Body -notmatch '_blazor')
    Assert 'A2 /club/{slug}/session/{id} -> 200'      ($rec.Status -eq 200)   "status=$($rec.Status)"
    Assert 'A2 session recap is static (no circuit)'  $recNoCircuit           'found interactive-circuit reference'
} else { Write-Output 'SKIP  A2 (no -SessionId supplied)' }

# 3. / -> served by the Site
$root = Get-Page "$baseTrim/"
Assert 'A3 / -> 200'                 ($root.Status -eq 200)                    "status=$($root.Status)"
Assert 'A3 / served by the Site'     ($root.Body -match [regex]::Escape($SiteMarker)) "Site marker '$SiteMarker' absent (was it proxied to the app?)"

# 4. /features -> served by the Site
$feat = Get-Page "$baseTrim/features"
Assert 'A4 /features -> 200'         ($feat.Status -eq 200)                    "status=$($feat.Status)"

# 5. boundary: /club-foo (no slash) must NOT be proxied -> Site handles it (404 from Site, not app 200)
$boundary = Get-Page "$baseTrim/club-foo"
Assert 'A5 /club-foo not over-matched by ^club(/.*)?$' ($boundary.Status -ne 200 -or ($boundary.Body -match [regex]::Escape($SiteMarker))) "unexpectedly proxied (status=$($boundary.Status))"

Write-Output ''
if ($fails -eq 0) { Write-Output 'FF-S6: ALL ASSERTIONS GREEN'; exit 0 }
else { Write-Output "FF-S6: $fails ASSERTION(S) FAILED"; exit 1 }
