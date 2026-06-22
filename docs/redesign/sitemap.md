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
4. Club Nights pillar - DONE (pick-explanation screenshot pending; full_court.jpg is low-res)
5. Features hub - DONE
6. Club Management pillar - DONE (built around the Insights/reports suite; member-mgmt screenshot illustrative)
7. Pricing - next
8. FAQ - next
9. About - next

Insights/reports suite (Manage Your Club): Session summary, Payment summary, Attendance trends, Partnerships, Members table, Per-member history, Head-to-head, Organiser insights - all treated as live by launch (confirmed 2026-06-20).

Shared design system extracted to `mockups/mockup.css` (all pages link it). Blue palette matched to the app (primary #1b6ec2, navy gradient #294f76→#1f3d5b).

**Pending screenshots:** Competitions (Select players, Generate pairs, Build groups, Group fixtures); League (Fixed partnerships, Team selection); Club Nights (Pick explanation, hi-res court display).

**Launch blocker:** real member names appear in payments / partnerships / standings / ratings screenshots - anonymise or use a demo club before going live.

Mockups are self-contained clickable HTML in `Docs/redesign/mockups/`. Screenshots and headline numbers are placeholders until real assets/figures are dropped in.
