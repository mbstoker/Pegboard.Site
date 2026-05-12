# Morning summary - Pegboard.Site web-version pivot

**Worked**: 2026-05-12 evening + late · **Commits**: 7 slices + summary, all pushed to origin

The plan lives at `Knowledge/marketing/site-revamp-plan.md` for context. This summary covers what got done overnight + what needs your eyes today.

> **Update**: Slice 7 also landed - web app screenshots captured + parallel Features/FeaturesDesktop pages. Details under "Slice 7" below.

---

## What's done

Six discrete commits, each reviewable in isolation. Pushed to `origin/main`.

| # | Commit | Slice | Files | Build |
|---|---|---|---|---|
| 1 | `9a94f34` | Homepage + nav layout | `Index.cshtml`, `_Layout.cshtml` | clean |
| 2 | `b7c134d` | FAQs + Pricing | `FAQs.cshtml`, `Pricing.cshtml` | clean |
| 3 | `91f477a` | Features page | `Features.cshtml` | clean |
| 4 | `6b3c7a5` | Roadmap + Release Notes | `ProductRoadmap.cshtml`, `ReleaseNotes.cshtml` | clean |
| 5 | `6c6cd6d` | Download flow + forms | `Download.cshtml`, `DownloadLink.cshtml`, `ReportBug.cshtml`(.cs), `RequestFeature.cshtml`(.cs) | clean |
| 6 | `c5b9cfa` | Privacy + License + sweep | `Privacy.cshtml`, `License.cshtml`, `ReleaseNotes.cshtml` (dashes) | clean |
| 7 | `8cd9285` | Web screenshots + parallel Features pages | `Index.cshtml`, `Features.cshtml`, `FeaturesDesktop.cshtml`(+`.cs`), 8x `site-*.png` | clean |

`dotnet build` is clean (0 errors) after every slice, including after `obj/Debug` is wiped (so the Razor source generator isn't masking anything).

### Anchor framings used consistently across all pages

- **CTA**: "Try Free at play.epegboard.com" / "Sign Up Free" (green button)
- **Pricing**: "Free for at least 60 days. No card details."
- **Caveat**: "Tablet or laptop-sized screens recommended for the best experience."
- **Picker claim**: "The most common complaint we heard was higher-rated players paired with weak partners - that's been the main driver of the picker work since."
- **Roadmap items**: "sticky" partner preferences, single-discipline sessions, singles support, more reporting, tournament support (internal + inter-club), phone display polish.
- **Style**: no em dashes, hyphens only. No "much-loved app".

### Decisions you made before I started

- Web app post-trial pricing: **TBC, announce ahead of any change** - applied to Pricing + FAQs + License.
- Desktop app positioning: **supported, but most new features web-only** - applied as a consistent line across Pricing, FAQs, Roadmap, ReleaseNotes, Download, License.
- Sign-up CTA: **direct link to play.epegboard.com, new tab** - applied everywhere.
- Deploy: **commits only, you deploy in the morning** - nothing has been deployed.

### Defaults I applied (per the handover)

- Voice/tone: matched the wave-1..5 email campaign.
- "Web vs Desktop" comparison: folded into FAQs as a Q ("Web app or desktop app - which should I pick?") rather than a standalone page.
- Screenshots: reused existing WinForms-era ones. Fresh capture is Slice 7, deferred.
- Public ProductRoadmap: lists the same items I've been promising in the email campaign for consistency.
- SEC-021 (MySQL password scrub): skipped as out of scope - separate security task.

---

## Slice 7 - web screenshots + parallel Features pages

After the first 6 slices landed, you asked me to "look at replacing the screenshots" and floated the parallel-pages idea. So:

**Screenshots** - captured 8 fresh shots of the web app via a new `SiteScreenshotCapture.cs` E2E test in PegboardWeb (commit `e1e4dc5` there). The test signs up a throwaway user, clicks "Explore Demo Club", starts 3 courts on the dashboard, then navigates through tabs + reports capturing PNGs:
- `site-dashboard.png` - mid-session, 4 courts, players with photos
- `site-completed-games.png` - games table with scores + par
- `site-attendee-stats.png` - W/L history, ratings, rating-change deltas
- `site-payments.png` - per-attendee payment tracking
- `site-members.png` - member admin with photos
- `site-attendance-trends.png` - Active/Lapsing/Dormant/New classification
- `site-session-summary.png` - cross-session view
- `site-payment-summary.png`

While I was capturing, I noticed the demo club had 4 stylised SVG avatars (alex/jordan/sam/taylor) but most members showed as colored initials. Generated 9 more SVG avatars (Riley/Lee/Nina/Dylan/Sofia/Marcus/Ivy/Owen/Maya) matching the existing pattern, wired them into `InMemoryDataSeeder`, deployed PegboardWeb to test, then re-ran the capture. The dashboard now shows a healthy mix of photos + colored initials, which looks more polished. All committed to PegboardWeb at `e1e4dc5` and deployed to `play.test`.

**Parallel pages** (your idea: "we may want parallel pages for some e.g. feature pages with winforms screenshots and features on one vs web and features on the other"):
- `Features.cshtml` - now the web-app features page. Uses new `site-*.png` shots for the 7 features where the web UI diverges materially from desktop (Real-time Stats, Attendance Reports, Payment Tracking, Member Management, Session Analytics, Completed Games History, Fairness Statistics). WinForms screenshots kept for features whose UI is essentially unchanged (Managing Attendees, Pick and Start, Prepare Next Game, etc.). 17 cards total.
- `FeaturesDesktop.cshtml` (new) - preserves the original WinForms-only content for clubs running the desktop app. Has a "Most new functionality is web" banner + cross-link to `/Features`.
- Both pages link to each other at the top.

Index.cshtml hero also got swapped from `Hero2.jpg` to `site-dashboard.png` - visitor immediately sees the actual web product on landing.

## What's NOT done

- **Deploy** - I have not deployed anything. Pegboard.Site has its own deploy pipeline (similar MSDeploy via WMSVC + IIS) that I haven't validated. You'll need to deploy, ideally via the same `IIS_Prod`-style publish profile if one exists, or via SCP fallback.
- **The mailto link in the new Privacy section's "Your rights" Qs** points at `admin@epegboard.com` - same as the old version. Confirm this still routes correctly.
- **Architecture.txt** in `src/` still has the old strategy ("ePegboard runs on your Windows tablet or laptop"). It's a planning doc, not user-facing, so left it alone. You may want to update for future Claude sessions.

---

## What to check this morning

Run the site locally (`dotnet run --project src/PegboardWebSite/PegboardWebSite.csproj`) and click through:

1. **Homepage** - hero is now the new mid-session `site-dashboard.png`. CTA opens play.epegboard.com in a new tab. Footer says © 2026. Top nav has a green "Sign Up Free" button on the right; "Download" is no longer in the top nav but is in the Help and Support dropdown.
2. **Features** (web) - 17 cards total. Half use new web screenshots, half retain WinForms-era ones where the UI didn't materially change. "Looking for desktop app features?" link at the top → `/FeaturesDesktop`.
2a. **FeaturesDesktop** - the legacy-style page with WinForms-only screenshots, "Most new functionality is web" banner, cross-link back to `/Features`.
3. **Pricing** - two-card layout, web (free for at least 60 days) and desktop (£60/year). Desktop purchase form still works.
4. **FAQs** - five sections now: General / Web app / 60-day free trial / Licensing / Desktop app. The "Web app or desktop app - which should I pick?" Q is the inline comparison.
5. **Download** - reframed as desktop-alternative with web app called out at the top.
6. **Roadmap** - shipped section + 4 coming-next sections. Items match what you've been emailing recipients.
7. **Release Notes** - "Web app" section at top with launch themes, "Desktop app" section below with the historical version notes (unchanged content, dashes swept).
8. **Report Bug / Request Feature** - "Which version?" dropdown on each form. Submit one to yourself to verify the new Version field comes through in the email body.
9. **Privacy** - three sections (Web app / Desktop app / Website). Web app section is the new one. Marked as interim pending paid legal review.
10. **License** - split into Web app / Desktop app / Both. Web app: free for at least 60 days, post-trial TBC.

---

## Open items / suggested next moves

After your review:

1. **Deploy** - decide on a deploy approach (MSDeploy via VS, SCP fallback, or set up a script). Worth taking a fresh backup of the live site first.
2. **Fresh screenshots (Slice 7)** - if you want them, capture and replace. Otherwise close out.
3. **Architecture.txt** - either update or remove (it's a planning doc Claude wrote in an earlier session - fine to leave but referring to the WinForms era).
4. **The MySQL password scrub (SEC-021)** - still on the security backlog. Tip-side replacement + history rewrite are both cheap right now while the repo's GitHub presence is fresh.
5. **`epegboard.app` domain decision** - if/when you want to add it, the Privacy + License pages need a one-line addition acknowledging that the web app is also reachable at the alternative domain.
6. **Paid legal review** - SEC-016 is the umbrella item. The Privacy + License "interim" framing is honest but should be replaced with the lawyered version when it's ready.

---

## Risk callouts

Things I'm specifically uncertain about and would value your eye on:

- **The new Version field in ReportBug + RequestFeature** - I added a nullable string property to the existing form models. The optional `[Required]`-less attribute should mean existing form submissions still work, but you might want to manually test both forms.
- **Privacy claim "encrypted in transit (HTTPS). Backed up daily; backups retained for 60 days"** - the HTTPS bit is verifiably true (the site has HTTPS bindings). The 60-day backup retention is what we set up in SEC-023 on the VPS. If that retention policy ever changes, the Privacy page needs to too.
- **Privacy claim "On our hosting infrastructure in the EU/UK"** - the VPS is IONOS hosted. I believe their UK/DE data centres are in scope, but worth verifying before this goes live to be defensible under GDPR.
- **The "Login cookies are session cookies; no third-party tracking" claim in Privacy** - true today (PegboardWeb has `epegboard.auth` cookie auth, no third-party trackers loaded). If you add analytics later, the Privacy page needs an update.
- **The image at `/Images/Hero2.jpg`** - I kept it as the homepage hero but it shows the WinForms version. A web-app hero image would be more on-brand for the new positioning. Cheap fix as part of Slice 7.

Sleep well. Hope it's useful in the morning.
