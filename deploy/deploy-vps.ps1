<#
.SYNOPSIS
    Deploy a published Pegboard.Site build to an IIS site on the ePegboard VPS.

.DESCRIPTION
    Runs ON the VPS. Overlays a self-contained publish (delivered as a .tar) onto an
    existing IIS site folder. To release the in-process AspNetCoreModuleV2 file locks
    on the binaries, it STOPS the app pool and waits for it to report Stopped before
    overwriting, then restarts it.

    History: this used to rely on an `app_offline.htm` + 3s drain. That was enough on
    the quiet staging site but NOT on the busy prod site (`pegboard.site`) - the worker
    still held the DLL locks after 3s, so `tar` failed with "Can't unlink already-existing
    object: Permission denied", the script threw, and `app_offline.htm` was left in place
    = prod outage (2026-06-25). Stopping the pool is the reliable lock-release mechanism.

    The site is net8.0 self-contained because the VPS has only the 7.0 and 10.0 ASP.NET
    runtimes installed (no 8.0) - so the publish MUST be:
        dotnet publish -c Release -r win-x64 --self-contained true
    Overlay (not wipe): the data-protection `keys\` folder and `logs\` are preserved.
    `appsettings.json` ships blank PegboardDb on purpose; the real connection string
    comes from the VPS env / applicationHost.config, so overwriting it is safe.

.PARAMETER Site   IIS site name, e.g. 'www.staging' (staging) or 'pegboard.site' (PROD).
.PARAMETER Tar    Path on the VPS to the publish tarball.
#>
param(
    [Parameter(Mandatory)][string]$Site,
    [Parameter(Mandatory)][string]$Tar
)
$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

$web = Get-Website -Name $Site
if (-not $web) { throw "No IIS site named '$Site'" }
$path = $web.PhysicalPath
$pool = $web.applicationPool
if (-not (Test-Path $Tar)) { throw "Tar not found: $Tar" }

$offline = Join-Path $path 'app_offline.htm'

Write-Output "Deploying to site '$Site' at $path (pool: $pool)"

# Friendly holding page for any in-flight request in the moment before the pool stops.
Set-Content $offline '<html><body>Updating, back shortly.</body></html>'

# Stop the app pool (not just app_offline) so the in-process worker exits and drops
# its DLL file handles before we overwrite the binaries.
if ((Get-WebAppPoolState -Name $pool).Value -ne 'Stopped') {
    Stop-WebAppPool -Name $pool
}
$deadline = 30
for ($i = 0; $i -lt $deadline; $i++) {
    if ((Get-WebAppPoolState -Name $pool).Value -eq 'Stopped') { break }
    Start-Sleep -Seconds 1
}
if ((Get-WebAppPoolState -Name $pool).Value -ne 'Stopped') {
    throw "App pool '$pool' did not reach Stopped within ${deadline}s - aborting before extract (app_offline.htm left in place; site needs attention)"
}
# Give the worker process a final moment to fully release its handles.
Start-Sleep -Seconds 3

& tar -xf $Tar -C $path
if ($LASTEXITCODE -ne 0) {
    throw "tar extract failed (exit $LASTEXITCODE) - pool left stopped and app_offline.htm in place; integrity unknown, do NOT auto-start, investigate on the box"
}

Remove-Item $offline -ErrorAction SilentlyContinue
Start-WebAppPool -Name $pool
Start-Sleep -Seconds 4
Write-Output ("Pool state: " + (Get-WebAppPoolState -Name $pool).Value)
Write-Output "Deployed '$Site' OK"
