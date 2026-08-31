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

Use a free loopback port, a writable disposable data-protection key directory and a non-production connection string. Do not point a local run at the live site database. The current root page depends on those settings; without them the process can listen successfully but `/` returns HTTP 500, which is not a passing smoke test.

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ASPNETCORE_URLS='http://127.0.0.1:5180'
$env:ConnectionStrings__DefaultConnection='<non-production PostgreSQL connection string>'
# Configure ASP.NET data-protection keys to a writable disposable local directory.
dotnet run --project src/PegboardWebSite/PegboardWebSite.csproj
Invoke-WebRequest http://127.0.0.1:5180/
```

The repository has no dedicated health endpoint; an HTTP 200 from `/` is the local liveness smoke. Concurrent server runs require different ports and independent non-production data configurations.

## Data, architecture and deployment

The site reads PostgreSQL-backed public data through Npgsql; it does not own schema migrations. Database schema and migration ownership stay with the application that owns the canonical data. Site structure and current redesign navigation are indexed in [`docs/README.md`](README.md) and [`docs/redesign/sitemap.md`](redesign/sitemap.md).

The deployable is a self-contained `win-x64` publish of `src/PegboardWebSite`. The canonical release procedure is [`deploy/README.md`](../deploy/README.md); Staging is mandatory before Production, and Mike controls both VPS deployment decisions.

## Current variances

### SITE-ENGINEERING-001 — no self-contained local liveness harness

- **Scope/owner:** local runtime verification; Growth.
- **Rule not met:** a safe, repository-owned local run/health path that works from committed non-secret configuration.
- **Reason:** the application starts on loopback, but `/` requires a writable data-protection key store and non-production PostgreSQL configuration; the repository has no dedicated dependency-free health endpoint or committed harness that provisions those inputs.
- **Risk/containment:** build and tests remain mandatory; runtime smoke evidence is required when an authorised non-production configuration is available, and production data must never be used as a shortcut.
- **Acceptance:** pending Mike; no variance is active until accepted.
- **Review/expiry:** review 2026-09-30; expire 2026-12-31 if accepted without remediation.
- **Removal condition:** provide a safe committed local harness or dependency-free health endpoint and verify it from a clean checkout.
