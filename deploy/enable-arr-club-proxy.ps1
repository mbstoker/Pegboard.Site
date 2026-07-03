<#
.SYNOPSIS
    ONE-TIME, server-level prerequisite for SEAM S6 (www/club/* -> PegboardWeb).
    Runs ON the VPS. Installs IIS URL Rewrite + Application Request Routing (ARR),
    enables the ARR proxy at server level, and whitelists the server variables the
    S6 rewrite rules set. Idempotent: safe to re-run.

    *** MIKE-GATED. This is a server-level change on the prod VPS (touches
        applicationHost.config + installs MSIs). Claude stages it; Mike runs it. ***

.DESCRIPTION
    The S6 reverse-proxy rules live in each marketing site's published web.config
    (src/PegboardWebSite/web.config). Those rules require, at the SERVER level:
      1. URL Rewrite module          (rule engine)
      2. Application Request Routing  (the proxy that forwards to an absolute URL)
      3. proxy.enabled = true         (ARR won't forward otherwise)
      4. allowedServerVariables       (HTTP_HOST + X-Forwarded-* the rules SET)
    None of these are installed today (confirmed 2026-07-02 read-only inspection:
    no Rewrite/ARR global modules, system.webServer/proxy section absent).

    This script installs 1-2 via the offline MSIs staged next to it (download step
    below), then configures 3-4. It does NOT deploy site content and does NOT touch
    prod site bindings.

.NOTES
    Download the two MSIs on this box (or Mike's) and scp them to C:\temp\_arr\ first:
      URL Rewrite 2.1:  https://download.microsoft.com/download/1/2/8/128E2E22-C1B9-44A4-BE2A-5859ED1D4592/rewrite_amd64_en-US.msi
      ARR 3.0:          https://download.microsoft.com/download/E/9/8/E9849D6A-020E-47E4-9FD0-A023E99B54EB/requestRouter_amd64.msi
    (ARR 3.0 bundles the External Cache dependency; install Rewrite FIRST, then ARR.)
#>
$ErrorActionPreference = 'Stop'
Import-Module WebAdministration

$stage = 'C:\temp\_arr'
$rewriteMsi = Join-Path $stage 'rewrite_amd64_en-US.msi'
$arrMsi     = Join-Path $stage 'requestRouter_amd64.msi'

function Install-Msi($path, $name) {
    if (-not (Test-Path $path)) { throw "MSI not staged: $path (scp it to $stage first)" }
    Write-Output "Installing $name ..."
    $p = Start-Process msiexec.exe -ArgumentList "/i `"$path`" /quiet /norestart" -Wait -PassThru
    if ($p.ExitCode -ne 0 -and $p.ExitCode -ne 3010) { throw "$name install failed (exit $($p.ExitCode))" }
    Write-Output "  $name OK (exit $($p.ExitCode))"
}

# 1-2. Modules (skip if already present) -------------------------------------
$mods = Get-WebGlobalModule | Select-Object -ExpandProperty Name
if ($mods -notcontains 'RewriteModule')  { Install-Msi $rewriteMsi 'URL Rewrite 2.1' } else { Write-Output 'URL Rewrite already installed.' }
if ($mods -notcontains 'ApplicationRequestRouting') { Install-Msi $arrMsi 'ARR 3.0' } else { Write-Output 'ARR already installed.' }

# 3. Enable the ARR proxy at server level ------------------------------------
Set-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' `
    -filter 'system.webServer/proxy' -name 'enabled' -value 'True'
# Don't rewrite the Host header at the ARR layer — our rule sets HTTP_HOST explicitly.
Set-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' `
    -filter 'system.webServer/proxy' -name 'preserveHostHeader' -value 'False'
Write-Output ('proxy.enabled = ' + (Get-WebConfigurationProperty -pspath 'MACHINE/WEBROOT/APPHOST' -filter 'system.webServer/proxy' -name 'enabled').Value)

# 4. Whitelist the server variables the S6 rules set -------------------------
$vars = 'HTTP_HOST','HTTP_X_FORWARDED_HOST','HTTP_X_FORWARDED_PROTO'
foreach ($v in $vars) {
    $existing = Get-WebConfiguration -pspath 'MACHINE/WEBROOT/APPHOST' `
        -filter "system.webServer/rewrite/allowedServerVariables/add[@name='$v']"
    if (-not $existing) {
        Add-WebConfiguration -pspath 'MACHINE/WEBROOT/APPHOST' `
            -filter 'system.webServer/rewrite/allowedServerVariables' `
            -value @{ name = $v }
        Write-Output "  whitelisted $v"
    } else { Write-Output "  $v already whitelisted" }
}

Write-Output ''
Write-Output 'ARR/Rewrite prerequisite complete. Deploy the site build carrying the S6 web.config next.'
