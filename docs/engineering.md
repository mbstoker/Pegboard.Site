# Pegboard.Site repository engineering overlay

**Status:** Advisory shadow; not governing  
**Class:** Authority  
**Owner:** Growth  
**Review:** 2026-09-30

This advisory overlay is designed to apply the venture [Engineering Standard](../../../Docs/Engineering%20Standard.md) at cutover. Until then, [`Operating/engineering.md`](../../../Operating/engineering.md) remains governing. The standard wins unless a variance is explicitly listed below.

## Repository, toolchain and commands

Pegboard.Site is the public marketing website. It targets .NET 8 and restores from NuGet.

```powershell
cd src
dotnet restore Web.sln
dotnet clean Web.sln -c Release
dotnet build Web.sln -c Release --no-restore
dotnet test Web.sln -c Release --no-build
```

The solution test command is the full required automated suite. Page or filter-specific runs are diagnostic only.

## Safe local runtime and health

Use a free loopback port. Do not point a local run at the live site database. The site degrades to seed/default behaviour when PostgreSQL is unavailable, so the root liveness smoke does not require a database connection; data-dependent work must supply an explicitly non-production `PegboardDb` connection.

```powershell
$siteRoot = (git rev-parse --show-toplevel).Trim()
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='http://127.0.0.1:5180'
dotnet run --project (Join-Path $siteRoot 'src/PegboardWebSite/PegboardWebSite.csproj')
```

From a second shell while the server is running:

```powershell
Invoke-WebRequest http://127.0.0.1:5180/
```

For data-dependent verification only:

```powershell
$env:ConnectionStrings__PegboardDb='<non-production PostgreSQL connection string>'
```

The repository has no dedicated health endpoint; an HTTP 200 from `/` is the local liveness smoke. Concurrent server runs require different ports and independent non-production data configurations.

## Data, architecture and deployment

The site reads PostgreSQL-backed public data through Npgsql; it does not own schema migrations. Database schema and migration ownership stay with the application that owns the canonical data. Site structure and current redesign navigation are indexed in [`docs/README.md`](README.md) and [`docs/redesign/sitemap.md`](redesign/sitemap.md).

The deployable is a self-contained `win-x64` publish of `src/PegboardWebSite`. The canonical release procedure is [`deploy/README.md`](../deploy/README.md); Staging is mandatory before Production, and Mike controls both VPS deployment decisions.

## Current variances

None accepted. Absence of a dedicated health endpoint is recorded as a repository fact; the root smoke and full HTTP test suite are the current executable signals.
