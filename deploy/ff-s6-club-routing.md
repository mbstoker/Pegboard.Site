# FF-S6 — `www/club/{slug}` routing / static-SSR contract test

**Seam:** S6 (ADR-0010) — `www` edge ↔ PegboardWeb render (#636).
**Owner of the guard:** Platform (this proxy) + Product (the `/club/*` static-SSR render).
**Required deliverable of the #636 joint slice** (ADR-0010 §Sign-off: "filed before it starts").

## Invariant under test (the two-backends-by-path contract)

On a marketing host (`www.staging.epegboard.com` in test; `www.epegboard.com` in prod):

1. `GET /club/{knownSlug}` → **200**, `Content-Type: text/html`, **complete server-rendered HTML** — the club name/heading present in the raw response body with **no dependency on JS or a SignalR/Blazor circuit** (assert the marker HTML is in the byte body, and that there is **no** `/_blazor` interactive-circuit `<script>` / `blazor.server.js` reference required to render it).
2. `GET /club/{knownSlug}/session/{id}` → **200**, same static-HTML property (recap page — needs #632 deployed to the fronted env; see Integration below).
3. `GET /` → **200** and served by **the Site** (assert a Site-only marker, e.g. the marketing hero / a `/features` nav link — content that does not exist on the app's club page).
4. `GET /features` → **200** and served by **the Site**.
5. Negative: `GET /club-foo` (no slash) is **NOT** proxied — it must fall through to the Site (404 from the Site, not the app), proving the `^club(/.*)?$` boundary doesn't over-match.

Pass = all five hold. Any failure blocks #636 signoff.

## Why this is an HTTP-level test, not a unit test

The routing lives in IIS (URL Rewrite + ARR), not in either app's managed code, so the
only faithful assertion is an end-to-end HTTP probe against a running env that has the
proxy configured. There is no in-process seam to unit-test on the Site side. (Contrast
FF-S1, which IS an in-process minimal-API test in the App repo.) The runnable form is
`ff-s6-club-routing.ps1`; the same assertions can be lifted into an xUnit
`HttpClient` test in the Site's first test project when FF-S3 creates it.

## Runnable form

`deploy/ff-s6-club-routing.ps1 -BaseUrl https://www.staging.epegboard.com -Slug <knownStagingSlug> -SessionId <id>`

Exit 0 = all assertions pass; non-zero + a per-assertion FAIL line otherwise.

## Preconditions

- ARR/Rewrite prerequisite applied on the box (`enable-arr-club-proxy.ps1`).
- The Site build carrying the S6 `web.config` deployed to the fronted marketing site.
- **#632 (the `/club/{slug}` + `/club/{slug}/session/{id}` static-SSR render) deployed
  to the app env the proxy fronts** — for staging that is `play.staging`. Until #632 is
  on `play.staging`, assertions 1-2 will 404/500 (the proxy works, but there is no
  render behind it). Assertions 3-5 (Site fall-through + boundary) are independently
  verifiable NOW and prove the proxy itself.

## Integration / joint close-out with #632

FF-S6 is **jointly green only after #632 is deployed to the fronted app env**. Sequence:

1. Platform: install ARR/Rewrite (Mike) + deploy Site build with S6 `web.config` to `www.staging`.
2. Product: deploy #632 static-SSR `/club/*` to `play.staging`.
3. Run `ff-s6-club-routing.ps1` against `www.staging` with a known staging club slug + session id → all 5 assertions green.
4. Promote to prod as a co-deploy (Site S6 web.config + #632) — Mike-gated (ADR-0010 §5, deploy sequencing).
