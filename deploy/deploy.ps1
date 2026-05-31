<#
.SYNOPSIS
    Build + FTPS-deploy Pegboard.Site to IONOS (staging or prod).

.DESCRIPTION
    1. Reads FTP credentials from .env (venture root, gitignored - never committed).
    2. dotnet publish -c Release to a clean temp folder.
    3. Uploads app_offline.htm (takes the ASP.NET Core app offline so its DLLs/exe
       aren't file-locked), optionally wipes the remote target (-Clean), uploads
       every published file, then removes app_offline.htm to bring the app back.

    Transfers use .NET System.Net.FtpWebRequest with EnableSsl (explicit FTPS,
    AUTH TLS on port 21) and passive mode. This was chosen over curl because
    IONOS rejects curl's -Q "DELE x" prequote (runs from login root -> 550);
    FtpWebRequest addresses each file by full URI and deletes/uploads reliably.

    Credentials come from .env and are held only in a NetworkCredential in memory.

.PARAMETER Target   'staging' (default) or 'prod'. Prod = www.epegboard.com.
.PARAMETER DryRun   Build + list what WOULD upload; make no FTP connection.
.PARAMETER Clean    Recursively delete the remote target's contents first (keeps
                    app_offline.htm during the wipe). Use when the folder holds
                    stale files from a different/previous app (e.g. /beta still
                    held an old Blazor build -> 500.35 multiple-apps error).
.PARAMETER NoAppOffline  Skip the app_offline.htm stop/start dance.

.EXAMPLE
    ./deploy/deploy.ps1 -Target staging -DryRun
    ./deploy/deploy.ps1 -Target staging -Clean
    ./deploy/deploy.ps1 -Target prod -Clean
#>
[CmdletBinding()]
param(
    [ValidateSet('staging','prod')]
    [string]$Target = 'staging',
    [switch]$DryRun,
    [switch]$Clean,
    [switch]$NoAppOffline
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$proj     = Join-Path $repoRoot 'src\PegboardWebSite\PegboardWebSite.csproj'

function Fail($m) { Write-Host "ERROR: $m" -ForegroundColor Red; exit 1 }
function Info($m) { Write-Host $m -ForegroundColor Cyan }
function Ok($m)   { Write-Host $m -ForegroundColor Green }

# Encode each path segment (spaces -> %20) but keep "/" separators, for the URI.
function Encode-RemotePath($rel) {
    ($rel -split '/' | ForEach-Object { [Uri]::EscapeDataString($_) }) -join '/'
}

# --- 1. Locate + load .env -----------------------------------------------------
# Search order: $env:PEGSITE_ENV -> venture root C:\Business\ePegboard\.env -> deploy\.env
$ventureRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$candidates = @($env:PEGSITE_ENV, (Join-Path $ventureRoot '.env'), (Join-Path $PSScriptRoot '.env')) | Where-Object { $_ }
$envFile = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $envFile) {
    Fail "No .env found. Looked for: $($candidates -join '; '). Copy deploy/.env.example to one of these and fill it in."
}
Info "Using credentials from: $envFile"

$cfg = @{}
foreach ($line in Get-Content $envFile) {
    $t = $line.Trim()
    if ($t -eq '' -or $t.StartsWith('#')) { continue }
    $i = $t.IndexOf('=')
    if ($i -lt 1) { continue }
    $cfg[$t.Substring(0,$i).Trim()] = $t.Substring($i+1).Trim()
}

$P     = $Target.ToUpper()
$host_ = $cfg["FTP_${P}_HOST"]
$user  = $cfg["FTP_${P}_USER"]
$pass  = $cfg["FTP_${P}_PASS"]
$path  = $cfg["FTP_${P}_PATH"]; if (-not $path) { $path = '/' }
$siteUrl = $cfg["FTP_${P}_URL"]

if (-not $host_ -or -not $user -or -not $pass) { Fail "Missing FTP_${P}_HOST / USER / PASS in $envFile" }
if (-not $path.StartsWith('/')) { $path = '/' + $path }
if (-not $path.EndsWith('/'))   { $path = $path + '/' }

$baseUri = "ftp://${host_}${path}"           # e.g. ftp://host/beta/
$cred    = New-Object System.Net.NetworkCredential($user, $pass)

Info "Target:   $Target"
Info "Endpoint: $baseUri  (explicit FTPS, user $user)"
if ($Clean) { Info "Clean:    YES - remote target contents wiped before upload" }

# --- FtpWebRequest helpers -----------------------------------------------------
function New-Req($uri, $method) {
    $r = [System.Net.FtpWebRequest]::Create($uri)
    $r.Credentials = $cred
    $r.EnableSsl   = $true           # explicit FTPS (AUTH TLS)
    $r.UsePassive  = $true
    $r.UseBinary   = $true
    $r.KeepAlive   = $true
    $r.Method      = $method
    return $r
}
function Ftp-ListDetails($uri) {
    # returns raw LIST lines (with <DIR>/size markers) for the dir at $uri (must end in /)
    try {
        $r = New-Req $uri ([System.Net.WebRequestMethods+Ftp]::ListDirectoryDetails)
        $resp = $r.GetResponse(); $sr = New-Object System.IO.StreamReader($resp.GetResponseStream())
        $txt = $sr.ReadToEnd(); $sr.Close(); $resp.Close()
        return ($txt -split "`r?`n" | Where-Object { $_ -ne '' })
    } catch { return @() }   # dir absent/empty
}
function Ftp-Delete($uri) {
    try { $r = New-Req $uri ([System.Net.WebRequestMethods+Ftp]::DeleteFile); $r.GetResponse().Close(); return $true }
    catch { return $false }
}
function Ftp-RemoveDir($uri) {
    try { $r = New-Req $uri ([System.Net.WebRequestMethods+Ftp]::RemoveDirectory); $r.GetResponse().Close(); return $true }
    catch { return $false }
}
function Ftp-MakeDir($uri) {
    try { $r = New-Req $uri ([System.Net.WebRequestMethods+Ftp]::MakeDirectory); $r.GetResponse().Close(); return $true }
    catch { return $false }   # already exists -> ignore
}
function Ftp-Upload($localFile, $uri) {
    $r = New-Req $uri ([System.Net.WebRequestMethods+Ftp]::UploadFile)
    $bytes = [System.IO.File]::ReadAllBytes($localFile)
    $r.ContentLength = $bytes.Length
    $s = $r.GetRequestStream(); $s.Write($bytes,0,$bytes.Length); $s.Close()
    $resp = $r.GetResponse(); $resp.Close()
}

# Bulk-upload via curl over a SINGLE connection (config file of -T/url pairs).
# FtpWebRequest's per-file UploadFile intermittently fails its TLS data channel
# ("System error" on GetRequestStream) against IONOS; curl's reused connection is
# reliable and fast. Local paths use forward slashes (backslash is an escape char
# in a curl -K config). Run under Continue: curl writes progress to stderr which
# would otherwise throw under ErrorActionPreference=Stop; judge by exit code.
function Curl-UploadBatch($items) {
    $batchCfg = Join-Path ([System.IO.Path]::GetTempPath()) "pegsite-curl-batch-$Target.cfg"
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("user = `"$user`:$pass`"")
    [void]$sb.AppendLine("ftp-create-dirs")
    [void]$sb.AppendLine("ssl-reqd")
    [void]$sb.AppendLine("--silent")
    [void]$sb.AppendLine("--show-error")
    foreach ($it in $items) {
        $u = $baseUri + (Encode-RemotePath $it.Rel)
        [void]$sb.AppendLine("-T `"$($it.Local.Replace('\','/'))`"")
        [void]$sb.AppendLine("url = `"$u`"")
    }
    Set-Content -Path $batchCfg -Value $sb.ToString() -Encoding ascii
    try {
        $prevEA = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
        & curl.exe -K $batchCfg 2>$null
        $rc = $LASTEXITCODE
        $ErrorActionPreference = $prevEA
        if ($rc -ne 0) { throw "curl batch upload failed (exit $rc)" }
    } finally { Remove-Item $batchCfg -Force -ErrorAction SilentlyContinue }
}

# Parse a LIST line -> @{ Name; IsDir }. Handles unix-style (drwx...) and the
# IIS/DOS style (MM-dd-yy hh:mmAM <DIR>|size name) IONOS returns.
function Parse-ListLine($line) {
    if ($line -match '^\s*\d{2}-\d{2}-\d{2}\s+\d{2}:\d{2}(AM|PM)\s+(<DIR>|\d+)\s+(.+?)\s*$') {
        return @{ Name = $matches[3].Trim(); IsDir = ($matches[2] -eq '<DIR>') }
    }
    if ($line -match '^([dl-])\S+\s+\S+\s+\S+\s+\S+\s+\d+\s+\S+\s+\S+\s+\S+\s+(.+?)\s*$') {
        return @{ Name = $matches[2].Trim(); IsDir = ($matches[1] -eq 'd') }
    }
    return $null
}

# Recursively delete contents of remote dir (dirUri ends in /). Skips app_offline.htm at top.
function Remove-RemoteTree($dirUri, $isTop) {
    foreach ($line in (Ftp-ListDetails $dirUri)) {
        $e = Parse-ListLine $line
        if (-not $e) { continue }
        if ($e.Name -in @('.','..')) { continue }
        if ($isTop -and $e.Name -eq 'app_offline.htm') { continue }
        $childUri = $dirUri + (Encode-RemotePath $e.Name)
        if ($e.IsDir) {
            Remove-RemoteTree ($childUri + '/') $false
            [void](Ftp-RemoveDir ($childUri + '/'))
        } else {
            [void](Ftp-Delete $childUri)
        }
    }
}

# --- 2. Publish ----------------------------------------------------------------
$pub = Join-Path ([System.IO.Path]::GetTempPath()) "pegsite-pub-$Target"
if (Test-Path $pub) { Remove-Item $pub -Recurse -Force }
Info "Publishing (Release) -> $pub"
& dotnet publish $proj -c Release -o $pub --nologo | Out-Null
if ($LASTEXITCODE -ne 0) { Fail "dotnet publish failed (exit $LASTEXITCODE)" }

$files = Get-ChildItem $pub -Recurse -File
Ok ("Published {0} files ({1:N1} MB)" -f $files.Count, (($files | Measure-Object Length -Sum).Sum / 1MB))

# --- 3. Dry run? ---------------------------------------------------------------
if ($DryRun) {
    Info "DRY RUN - these files would upload to ${baseUri}:"
    $files | ForEach-Object { "  " + $_.FullName.Substring($pub.Length+1).Replace('\','/') } | Write-Host
    if ($Clean) { Info "(-Clean) would first recursively delete existing contents of $path" }
    Ok "Dry run complete. No FTP connection made."
    exit 0
}

# --- 4. app_offline.htm (take app offline) ------------------------------------
if (-not $NoAppOffline) {
    $offline = Join-Path ([System.IO.Path]::GetTempPath()) 'app_offline.htm'
    Set-Content $offline -Value '<html><body><h1>ePegboard - brief update in progress, back in a moment.</h1></body></html>' -Encoding utf8
    Info "Uploading app_offline.htm (taking app offline)"
    Ftp-Upload $offline ($baseUri + 'app_offline.htm')
    Start-Sleep -Seconds 2
}

# --- 5. Optional clean ---------------------------------------------------------
if ($Clean) {
    Info "Cleaning remote target $path ..."
    Remove-RemoteTree $baseUri $true
    Ok "Remote target wiped."
}

# --- 6. Upload everything (curl, single connection; ftp-create-dirs makes paths) ---
Info "Uploading $($files.Count) files..."
$items = $files | ForEach-Object { @{ Local = $_.FullName; Rel = $_.FullName.Substring($pub.Length+1).Replace('\','/') } }
Curl-UploadBatch $items
Ok "Uploaded $($files.Count) files."

# --- 7. Bring app back online --------------------------------------------------
if (-not $NoAppOffline) {
    Info "Removing app_offline.htm (bringing app online)"
    if (Ftp-Delete ($baseUri + 'app_offline.htm')) { Ok "app_offline.htm removed." }
    else { Write-Host "WARNING: could not remove app_offline.htm - site may still show the offline page. Remove it manually." -ForegroundColor Yellow }
}

Ok "Deploy to $Target complete."
if ($siteUrl) { Info "Verify: $siteUrl" }
exit 0
