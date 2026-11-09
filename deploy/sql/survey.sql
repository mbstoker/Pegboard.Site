-- Organiser and player survey storage for the marketing site (ado:2566).
-- Target: VPS PostgreSQL 18, per-env DB (pegboard.stag for staging, pegboard.prod for prod).
-- Apply with:  psql -d pegboard.stag -f survey.sql
--          or: psql -d pegboard.prod -f survey.sql
-- Idempotent + additive: safe to re-run.
-- (The site also creates these on startup via NpgsqlSurveyStore.EnsureSchemaAsync; keep the
--  two definitions in step.)
--
-- Separation (survey brief section 8): the two tables share no key and no reference.
-- Ids are random UUIDs, not sequences, and a tester row's time is kept to the day, so a
-- tester row cannot be lined up with a response by order or time either.

-- One row per completed survey. No name, no email, no IP address.
CREATE TABLE IF NOT EXISTS survey_responses (
    id                 UUID        PRIMARY KEY, -- the survey run's id, fixed at the opening page; inserts are ON CONFLICT (id) DO NOTHING
    survey_id          TEXT        NOT NULL,   -- 'organisers' | 'players'
    definition_version INTEGER     NOT NULL,   -- the version the respondent answered (a version change restarts the survey)
    submitted_utc      TIMESTAMPTZ NOT NULL,   -- rounded down to the hour
    answers            JSONB       NOT NULL,   -- {"S1": ["UK"], "N2": ["Round robin", "Ladder"], ...}; hidden questions never present
    path               JSONB       NOT NULL    -- ids of the questions shown, in order
);

CREATE INDEX IF NOT EXISTS ix_survey_responses_survey ON survey_responses (survey_id, submitted_utc);

-- People who volunteered to test after submitting. Nothing here refers to a response.
CREATE TABLE IF NOT EXISTS survey_testers (
    id          UUID        PRIMARY KEY,
    email       TEXT        NOT NULL,          -- trimmed, lower-cased
    survey_id   TEXT        NOT NULL,          -- which survey's thank-you page it came from
    created_utc TIMESTAMPTZ NOT NULL           -- midnight UTC of the signup day
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_survey_testers_email_survey ON survey_testers (email, survey_id);
