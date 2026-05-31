-- Tracking table for the marketing site (visit / email-open / download tracking).
-- Target: VPS PostgreSQL 18, per-env DB (pegboard.stag for staging, pegboard.prod for prod).
-- Apply with:  psql -d pegboard.stag -f tracked_requests.sql
--          or: psql -d pegboard.prod -f tracked_requests.sql
-- Idempotent: safe to re-run.

CREATE TABLE IF NOT EXISTS tracked_requests (
    id                 SERIAL PRIMARY KEY,
    tracker_id         TEXT        NULL,        -- the ?t= / recipientId attribution token
    request_time       TIMESTAMPTZ NOT NULL,
    requested_resource TEXT        NULL,        -- "Home Page", "Email Open", a download filename, etc.
    source_ip          TEXT        NULL
);

-- Common query is "recent activity, optionally filtered by tracker" -> index by time + tracker.
CREATE INDEX IF NOT EXISTS ix_tracked_requests_time    ON tracked_requests (request_time DESC);
CREATE INDEX IF NOT EXISTS ix_tracked_requests_tracker ON tracked_requests (tracker_id);
