-- Re-stamp the timestamps that were written while the API ran on a UTC host.
--
-- Every timestamp column here is a bare TIMESTAMP, so its value only means
-- something relative to an assumed zone. The API writes Mountain wall clock
-- (see Services/Core/AppClock.cs), but before that fix it used DateTime.Now on
-- MRTN-LAPPS, which is set to Etc/UTC. Rows written in that period are six or
-- seven hours ahead of what they should say, which pushed anything entered
-- after 6pm Mountain onto the next day's square in the dashboard heatmaps.
--
-- WHICH ROWS: only those from 2026-09-09 onward. The rows from 2025-07 were
-- written before the move to MRTN-LAPPS and are already Mountain wall clock --
-- re-stamping those would shift correct data backwards. The two eras are
-- separated by a 13-month gap with no rows in it, so 2026-01-01 is a safe
-- cutoff anywhere in that gap. Their hour-of-day distributions confirm the
-- split: 2025-07 peaks at 6-9am and 6-9pm local, while 2026-09 clusters at
-- stored hours 2-5, which is 8-11pm Mountain.
--
-- AT TIME ZONE does the conversion with the right DST offset per row, so this
-- stays correct across the MDT/MST boundary rather than assuming a flat -6.
--
-- Run on mrtn-psql (10.0.0.225) against kc_db. Take a dump first:
--     pg_dump -h 10.0.0.225 -U kc_user -d kc_db -f kc_db_pre_tz_fix.sql

\set cutoff '2026-01-01'

BEGIN;

UPDATE "LogEntries"
SET "EntryDate" = ("EntryDate" AT TIME ZONE 'UTC') AT TIME ZONE 'America/Denver'
WHERE "EntryDate" >= :'cutoff'::timestamp;

UPDATE "Actions"
SET "CreatedAt" = ("CreatedAt" AT TIME ZONE 'UTC') AT TIME ZONE 'America/Denver'
WHERE "CreatedAt" >= :'cutoff'::timestamp;

UPDATE "Actions"
SET "CompletedAt" = ("CompletedAt" AT TIME ZONE 'UTC') AT TIME ZONE 'America/Denver'
WHERE "CompletedAt" >= :'cutoff'::timestamp;

-- Not heatmap data, but left skewed these would disagree with the rows above.
UPDATE "Domains"
SET "CreatedAt" = ("CreatedAt" AT TIME ZONE 'UTC') AT TIME ZONE 'America/Denver'
WHERE "CreatedAt" >= :'cutoff'::timestamp;

UPDATE "Domains"
SET "LastUsed" = ("LastUsed" AT TIME ZONE 'UTC') AT TIME ZONE 'America/Denver'
WHERE "LastUsed" >= :'cutoff'::timestamp;

UPDATE "Domains"
SET "ArchivedAt" = ("ArchivedAt" AT TIME ZONE 'UTC') AT TIME ZONE 'America/Denver'
WHERE "ArchivedAt" >= :'cutoff'::timestamp;

UPDATE "KnowledgeNodes"
SET "CreatedAt" = ("CreatedAt" AT TIME ZONE 'UTC') AT TIME ZONE 'America/Denver'
WHERE "CreatedAt" >= :'cutoff'::timestamp;

UPDATE "KnowledgeNodes"
SET "LastUpdated" = ("LastUpdated" AT TIME ZONE 'UTC') AT TIME ZONE 'America/Denver'
WHERE "LastUpdated" >= :'cutoff'::timestamp;

UPDATE "KnowledgeNodes"
SET "ArchivedAt" = ("ArchivedAt" AT TIME ZONE 'UTC') AT TIME ZONE 'America/Denver'
WHERE "ArchivedAt" >= :'cutoff'::timestamp;

COMMIT;
