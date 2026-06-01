# ratedclubs.com placeholder

Static "coming soon" landing page for ratedclubs.com (the parked phase-2 product,
registered 2026-05-24). Deployed to the VPS 2026-06-01 as IIS site `ratedclubs`
(`C:\sites\ratedclubs`, own app pool, static — no .NET runtime).

- Bindings: ratedclubs.com + www.ratedclubs.com, :80 + :443.
- TLS: Let's Encrypt via win-acme (auto-renew), same pattern as the epegboard sites.
- `index.html` is the whole site (self-contained, inline CSS). `web.config` sets the
  default doc + disables directory browsing. Page is noindex'd until launch.

To update: edit index.html, scp to C:\sites\ratedclubs\index.html on the VPS
(static — no app pool recycle needed). Replace wholesale when the real product ships.
