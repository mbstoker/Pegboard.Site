# ePegboard public site - redesign sitemap & IA

_Agreed 2026-06-20. Principle: **organise around the problems clubs are solving, not around software modules.**_

## Primary navigation (deliberately small)

`Home` · `Features` · `Pricing` · `FAQ` · `About` · **`Start Free Trial`** (button → play.epegboard.com/signup)

## Structure

```
Home
│
├── Features (hub - directory of the 4 pillars)
│   ├── Club Nights            /features/club-nights
│   ├── Club Management        /features/club-management
│   ├── Competitions           /features/competitions
│   └── League Team Support    /features/league-team-support
│
├── Pricing
├── FAQ
├── About
└── Free Trial (external → play.epegboard.com/signup)
```

Legacy/secondary pages retained but demoted out of primary nav (footer or Resources): Desktop app, Release notes, Product roadmap, Report bug, Request feature, Contact, Privacy, License, Downloads.

## The four pillars

| Pillar | Customer goal | Headline features |
|---|---|---|
| **Run Better Club Nights** | "Can this help me run my session?" | Smart picking, fairness tracking, pick explanations, results & stats, audio announcements |
| **Manage Your Club** | "Can this handle the admin?" | Attendance, payments, member management, reports & analytics |
| **Competitions & Tournaments** | "Can it run our internal tournament?" | Handicap tournaments, balanced pair generation, group stages, knockout rounds, live standings |
| **League Team Support** | "Can it help me pick teams?" | Partnership analytics, fixed partnerships, team-selection assistant |

## The rating engine (foundational, never a pillar)

One rating system powers all four pillars. Marketed **by outcome, not mechanism**:
- Stronger opponents are worth more; narrow defeats hurt less; surprise wins are rewarded; ratings update after every game.
- The same ratings drive: smart picking · handicap tournaments · partnership analytics · league selection.
- **Never** publish weighting curves, signals, or thresholds (picker is the moat).

Lives as a supporting section on the Club Nights pillar page + referenced briefly across the others. No dedicated ELO page.

## Homepage flow

Hero → Four pillars → Rating engine (the thread that ties them together) → How it works → Social proof → Pricing/trial teaser → Final CTA.

## Copy rules (house standards)

- Plain hyphens, never em-dashes. No sycophancy. Subjects/CTAs carry the brand.
- Lead with picker/fairness/outcome value, not technology.
- No "first/beta/brave" positioning with active users in production.

## Build order (mockups)

1. **Home** (design anchor - locks the visual language) - DONE
2. Competitions pillar - DONE
3. League Team Support pillar - DONE (Fixed Partnerships + Team Selection ship by launch; screenshots pending)
4. Club Nights pillar - DONE (hi-res court display + attendee-stats leaderboard + pick-explanation all captured from Riverside; pick-explanation cropped to the outcome overview - factor weights deliberately excluded per the moat rule, see backlog #532)
5. Features hub - DONE
6. Club Management pillar - DONE (built around the Insights/reports suite; member-mgmt screenshot illustrative). Reports gallery reworked 2026-06-23 into 3 role lanes - committee / membership / captains+players - surfacing ALL shipped reports grouped by who they help. ONLY GA reports advertised (the 8 `LiveCard` ones + per-member history / head-to-head); the 5 `SoonCard` reports (Match quality, Playing opportunity, Court utilisation, Most improved, Social mixing) stay off the site until they ship.
7. Pricing - next (gated on the open pricing-model decision)
8. FAQ - DONE (pricing answers flagged [placeholder] pending the pricing decision)
9. About - DONE (founder name / founding year flagged [placeholder] for Mike to confirm)

Insights/reports suite (Manage Your Club): Session summary, Payment summary, Attendance trends, Partnerships, Members table, Per-member history, Head-to-head, Organiser insights - all treated as live by launch (confirmed 2026-06-20).

Shared design system extracted to `mockups/mockup.css` (all pages link it). Blue palette matched to the app (primary #1b6ec2, navy gradient #294f76→#1f3d5b).

**Real screenshots captured 2026-06-22** from Riverside Badminton Club on `play.test` (synthetic test data - no real PII, so the names-anonymisation blocker below is RESOLVED for these). Replaced the placeholders for: `site-dashboard.png` (live 4-court session board), `standings.png` (competition group tables + matches), `partnerships.png` (partnership analytics, 5694 pairs), `attendance-trends.png` (active/lapsing/dormant + drift map), `ratings.png` (member profiles + ratings/win%), `full_court.png` (in-session board), `phone-session.png` (live session on a phone). Source PNGs + manifest in `Scratch/riverside-shots/`; capture harness in `Scratch/riverside-capture/` (standalone Playwright console, login `claude.shots@demo.pegboard.local`).

**Still pending screenshots:** Competition setup wizard (Select players, Generate pairs, Build groups, Group fixtures); League (Fixed partnerships, Team selection - ship by launch).

**~~Launch blocker~~ (resolved for captured shots):** the captured Riverside data is synthetic (procedural names), so the payments / partnerships / standings / ratings shots are safe to show. Re-check any FUTURE screenshots taken from a real club.

Mockups are self-contained clickable HTML in `Docs/redesign/mockups/`. Screenshots and headline numbers are placeholders until real assets/figures are dropped in.
