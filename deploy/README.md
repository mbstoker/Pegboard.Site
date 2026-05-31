# Deploying Pegboard.Site to IONOS

The marketing site (`www.epegboard.com`) is hosted on IONOS and deployed by FTP.
`deploy/deploy.ps1` builds a Release publish and uploads it over FTPS.

## One-time setup

1. Copy the template and fill in the real IONOS credentials:
   ```powershell
   Copy-Item deploy/.env.example deploy/.env
   ```
   Edit `deploy/.env` with the host / user / pass / remote path for **staging**
   and **prod** (the same details you use in FileZilla). `deploy/.env` is
   gitignored and must never be committed.

## Deploying

Always deploy to **staging first**, smoke-check it, then prod.

```powershell
# See exactly what would upload, without connecting:
./deploy/deploy.ps1 -Target staging -DryRun

# Deploy to staging:
./deploy/deploy.ps1 -Target staging

# After verifying staging, deploy to prod:
./deploy/deploy.ps1 -Target prod
```

## What the script does

1. Reads `deploy/.env`.
2. `dotnet publish -c Release` to a clean temp folder.
3. Uploads `app_offline.htm` first (stops the ASP.NET Core app so its DLLs aren't
   file-locked during upload), uploads every published file via `curl` over FTPS,
   then deletes `app_offline.htm` to bring the app back. Use `-NoAppOffline` to
   skip this (faster, but risks file locks on a live site).

Credentials are passed to curl via a temp config file, never on the command line,
so they don't appear in process listings or logs.

## Notes

- `appsettings.json` (with the prod MySQL connection string) is part of the
  publish output and will be uploaded. If staging needs different settings,
  manage that with an `appsettings.Staging.json` on the server or a separate
  publish - flag it and we'll wire it in.
- IONOS .NET hosting recycles the app pool when files change; the
  `app_offline.htm` dance makes that clean rather than racing live requests.
