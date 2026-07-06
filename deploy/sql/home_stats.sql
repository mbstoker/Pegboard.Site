-- Homepage "social proof" aggregate stats store for the marketing site.
-- Single row (id = 1) holding the three headline counts, refreshed weekly by the
-- in-app StatsRefreshService bake. Seeded with the current honest figures so a cold
-- start (or an unreachable diagnostics API) still renders sane values.
-- Target: VPS PostgreSQL 18, per-env DB (pegboard.stag for staging, pegboard.prod for prod).
-- Apply with:  psql -d pegboard.stag -f home_stats.sql
--          or: psql -d pegboard.prod -f home_stats.sql
-- Idempotent + additive: safe to re-run; never overwrites a live value.
-- (The app also self-bootstraps this schema on startup via HomeStatsRepository.EnsureSchemaAndSeed;
--  this script is the canonical hand-applied definition for the VPS.)

CREATE TABLE IF NOT EXISTS home_stats (
    id             SMALLINT    PRIMARY KEY,               -- always 1 (single-row store)
    games_played   BIGINT      NOT NULL,
    sessions_run   BIGINT      NOT NULL,
    players_rated  BIGINT      NOT NULL,
    source         TEXT        NOT NULL DEFAULT 'seed',   -- 'seed' | 'live'
    fetched_at_utc TIMESTAMPTZ NULL,                      -- when the last successful live fetch ran
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Seed the current honest figures. ON CONFLICT DO NOTHING so re-running never
-- clobbers a value the weekly bake has since updated.
INSERT INTO home_stats (id, games_played, sessions_run, players_rated, source, updated_at_utc)
VALUES (1, 33000, 1100, 2000, 'seed', now())
ON CONFLICT (id) DO NOTHING;
