# Deploying Pegboard.Site to the VPS

The marketing site is hosted as an **IIS site on the ePegboard VPS** (`217.154.118.7`,
reachable as `ssh vps`), fronted by IIS URL Rewrite + ARR:

| Env      | IIS site name  | Public host                    |
|----------|----------------|--------------------------------|
| Staging  | `www.staging`  | `www.staging.epegboard.com`    |
| Prod     | `pegboard.site`| `www.epegboard.com` (+ apex)   |

> **History:** the site used to live on IONOS shared hosting, deployed by FTPS
> (`deploy.ps1`). That was retired 2026-07-30 — the club reverse-proxy and the
> `/live` redirect both need server-level URL Rewrite/ARR, which shared hosting
> can't provide, so the site moved onto the VPS IIS. The old FTP script and its
> `.env.example` are gone; the VPS is the only deploy path now.

## Deploy mechanism

Deploying is a **build-local → transfer → extract-on-VPS** flow. `deploy-vps.ps1`
is the on-box half: it stops the app pool (to release the in-process DLL locks),
`tar`-extracts the publish over the site folder, and restarts the pool. It is
**Mike-gated** — it runs on the prod VPS.

The publish **must** be self-contained win-x64: the VPS has only the ASP.NET 7.0
and 10.0 runtimes installed (no 8.0), and the site targets net8.0.

```powershell
# 1. Publish (self-contained — REQUIRED; see note above) to a clean temp folder.
dotnet publish src/PegboardWebSite/PegboardWebSite.csproj `
    -c Release -r win-x64 --self-contained true -o $env:TEMP\pegsite-pub

# 2. Pack the publish into a tar.
tar -cf $env:TEMP\pegsite.tar -C $env:TEMP\pegsite-pub .

# 3. Copy it to the VPS.
scp $env:TEMP\pegsite.tar vps:C:/temp/pegsite.tar

# 4. On the VPS, overlay it onto the target site (STAGING FIRST, then prod).
ssh vps "powershell -File C:\path\to\deploy-vps.ps1 -Site www.staging  -Tar C:\temp\pegsite.tar"
# after smoke-checking staging:
ssh vps "powershell -File C:\path\to\deploy-vps.ps1 -Site pegboard.site -Tar C:\temp\pegsite.tar"
```

Always deploy to **staging first**, smoke-check, then prod.

## What `deploy-vps.ps1` does (the on-box half)

1. Writes a holding `app_offline.htm`, then **stops the app pool** and waits (up to
   30s) for `Stopped` — stopping the pool, not just `app_offline`, is what reliably
   releases the worker's DLL handles (the 3s-drain approach caused a prod outage
   2026-06-25 when the busy site still held locks).
2. `tar -xf` the publish over the site folder (overlay, not wipe — the
   data-protection `keys\` and `logs\` folders survive).
3. Removes `app_offline.htm` and restarts the pool.

`appsettings.json` ships with a blank connection string on purpose; the real
connection string comes from the VPS env / `applicationHost.config`, so
overwriting it on deploy is safe.

## Server-level prerequisites (one-time, already done on the VPS)

- **URL Rewrite** module — required by every rewrite rule in `web.config`.
- **ARR** with the proxy enabled — required only by the club reverse-proxy rules
  (the `/live` redirect needs Rewrite alone). See `enable-arr-club-proxy.ps1`.
