<#
.SYNOPSIS
    Deploy a published Pegboard.Site build to an IIS site on the ePegboard VPS.

.DESCRIPTION
    Runs ON the VPS. Overlays a self-contained publish (delivered as a .tar) onto an
    existing IIS site folder using the app_offline.htm graceful-drain pattern so the
    in-process AspNetCoreModuleV2 releases its file locks before the binaries are
    replaced, then recycles the app pool.

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
if (-not (Test-Path $Tar)) { throw "Tar not found: $Tar" }

Write-Output "Deploying to site '$Site' at $path (pool: $($web.applicationPool))"
Set-Content (Join-Path $path 'app_offline.htm') '<html><body>Updating, back shortly.</body></html>'
Start-Sleep -Seconds 3
& tar -xf $Tar -C $path
if ($LASTEXITCODE -ne 0) { throw "tar extract failed (exit $LASTEXITCODE) - app_offline left in place" }
Remove-Item (Join-Path $path 'app_offline.htm') -ErrorAction SilentlyContinue
Restart-WebAppPool -Name $web.applicationPool
Start-Sleep -Seconds 4
Write-Output "Deployed '$Site' OK"
