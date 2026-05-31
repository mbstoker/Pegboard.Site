<#
.SYNOPSIS
    Build + FTP-deploy Pegboard.Site to IONOS (staging or prod).

.DESCRIPTION
    1. Reads FTP credentials from deploy/.env (gitignored - never committed).
    2. dotnet publish -c Release to a clean temp folder.
    3. Uploads app_offline.htm first (stops the ASP.NET Core app so its DLLs/exe
       aren't file-locked during upload), uploads every published file via curl,
       then removes app_offline.htm.

    PROTOCOL handling (IONOS uses explicit FTPS on port 21):
      - "ftps"          -> explicit FTPS: curl ftp://host:port/  + --ssl-reqd  (port 21)
      - "ftps-implicit" -> implicit FTPS: curl ftps://host:port/             (port 990)
      - "ftp"           -> plain FTP:     curl ftp://host:port/
      - "sftp"          -> SFTP/SSH:      curl sftp://host:port/

    Credentials are passed to curl via a temp config file, never on the command
    line, so they don't appear in process listings or logs.

.PARAMETER Target   'staging' (default) or 'prod'. Prod = www.epegboard.com.
.PARAMETER DryRun   Build + list what WOULD upload, but make no FTP connection.
.PARAMETER NoAppOffline  Skip the app_offline.htm stop/start dance.

.EXAMPLE
    ./deploy/deploy.ps1 -Target staging -DryRun
    ./deploy/deploy.ps1 -Target staging
    ./deploy/deploy.ps1 -Target prod
#>
[CmdletBinding()]
param(
    [ValidateSet('staging','prod')]
    [string]$Target = 'staging',
    [switch]$DryRun,
    [switch]$NoAppOffline
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$proj     = Join-Path $repoRoot 'src\PegboardWebSite\PegboardWebSite.csproj'
$envFile  = Join-Path $PSScriptRoot '.env'
$curl     = 'curl.exe'

function Fail($m) { Write-Host "ERROR: $m" -ForegroundColor Red; exit 1 }
function Info($m) { Write-Host $m -ForegroundColor Cyan }
function Ok($m)   { Write-Host $m -ForegroundColor Green }

# --- 1. Load .env --------------------------------------------------------------
if (-not (Test-Path $envFile)) {
    Fail "deploy/.env not found. Copy deploy/.env.example to deploy/.env and fill it in."
}
$cfg = @{}
foreach ($line in Get-Content $envFile) {
    $t = $line.Trim()
    if ($t -eq '' -or $t.StartsWith('#')) { continue }
    $i = $t.IndexOf('=')
    if ($i -lt 1) { continue }
    $cfg[$t.Substring(0,$i).Trim()] = $t.Substring($i+1).Trim()
}

$P     = $Target.ToUpper()  # STAGING | PROD
$proto = $cfg["FTP_${P}_PROTOCOL"]; if (-not $proto) { $proto = 'ftps' }
$proto = $proto.ToLower()
$host_ = $cfg["FTP_${P}_HOST"]
$port  = $cfg["FTP_${P}_PORT"]
$user  = $cfg["FTP_${P}_USER"]
$pass  = $cfg["FTP_${P}_PASS"]
$path  = $cfg["FTP_${P}_PATH"]; if (-not $path) { $path = '/' }
$siteUrl = $cfg["FTP_${P}_URL"]

if (-not $host_ -or -not $user -or -not $pass) {
    Fail "Missing FTP_${P}_HOST / USER / PASS in deploy/.env"
}
if (-not $path.StartsWith('/')) { $path = '/' + $path }
if (-not $path.EndsWith('/'))   { $path = $path + '/' }

# Default ports per protocol if not specified
if (-not $port) {
    switch ($proto) {
        'ftps-implicit' { $port = 990 }
        'sftp'          { $port = 22 }
        default         { $port = 21 }
    }
}

# curl URL scheme: explicit FTPS still uses the ftp:// scheme (TLS via --ssl-reqd).
switch ($proto) {
    'ftps'          { $scheme = 'ftp';  $sslReqd = $true  }   # explicit FTPS (port 21)
    'ftps-implicit' { $scheme = 'ftps'; $sslReqd = $false }   # implicit FTPS (port 990)
    'sftp'          { $scheme = 'sftp'; $sslReqd = $false }
    'ftp'           { $scheme = 'ftp';  $sslReqd = $false }
    default         { Fail "Unknown protocol '$proto' (use ftps | ftps-implicit | ftp | sftp)" }
}
$baseUrl = "${scheme}://${host_}:${port}${path}"

Info "Target:   $Target"
Info "Endpoint: $baseUrl  (protocol $proto, user $user)"

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
    Info "DRY RUN - these files would upload to ${baseUrl}:"
    $files | ForEach-Object { "  " + $_.FullName.Substring($pub.Length+1).Replace('\','/') } | Write-Host
    Ok "Dry run complete. No FTP connection made."
    exit 0
}

# --- 4. curl config (keeps password off the command line) ----------------------
$curlCfg = Join-Path ([System.IO.Path]::GetTempPath()) "pegsite-curl-$Target.cfg"
$cfgLines = @(
    "user = `"$user`:$pass`""
    "ftp-create-dirs"
    "--silent"
    "--show-error"
)
if ($sslReqd) { $cfgLines += "ssl-reqd" }   # force AUTH TLS on explicit FTPS
Set-Content -Path $curlCfg -Value $cfgLines -Encoding ascii

function Upload-One($localFile, $remoteRel) {
    $url = $baseUrl + $remoteRel
    & $curl -K $curlCfg -T $localFile $url
    if ($LASTEXITCODE -ne 0) { throw "upload failed: $remoteRel (curl exit $LASTEXITCODE)" }
}

try {
    # --- 5. app_offline.htm: stop the app so files aren't locked ---------------
    if (-not $NoAppOffline) {
        $offline = Join-Path ([System.IO.Path]::GetTempPath()) 'app_offline.htm'
        Set-Content $offline -Value '<html><body><h1>ePegboard - brief update in progress, back in a moment.</h1></body></html>' -Encoding utf8
        Info "Uploading app_offline.htm (taking app offline)"
        Upload-One $offline 'app_offline.htm'
        Start-Sleep -Seconds 2
    }

    # --- 6. Upload everything --------------------------------------------------
    Info "Uploading $($files.Count) files..."
    $n = 0
    foreach ($f in $files) {
        $rel = $f.FullName.Substring($pub.Length+1).Replace('\','/')
        Upload-One $f.FullName $rel
        $n++
        if ($n % 10 -eq 0) { Write-Host "  ...$n/$($files.Count)" }
    }
    Ok "Uploaded $n files."

    # --- 7. Bring app back online ---------------------------------------------
    if (-not $NoAppOffline) {
        Info "Removing app_offline.htm (bringing app online)"
        & $curl -K $curlCfg -Q "DELE ${path}app_offline.htm" "$baseUrl" 2>$null
        # ignore DELE result; some servers report it oddly even on success
    }
}
finally {
    Remove-Item $curlCfg -Force -ErrorAction SilentlyContinue
}

Ok "Deploy to $Target complete."
if ($siteUrl) { Info "Verify: $siteUrl" }
