-- An ended season keeps the stats it had when it ended, so moving a Knowledge
-- Node, log or action to another domain later doesn't rewrite a finished
-- season's report. Written by the end and rotate endpoints; seasons ended
-- before this (or with no snapshot for any reason) fall back to live stats.
-- Additive and safe to re-run.
--
-- Run on mrtn-psql itself (the repo isn't there, so copy this file over first:
--   scp SQL/add_season_stats_snapshot.sql matt@10.0.0.225:~/ ). pg_hba only
-- allows kc_user over localhost on that box, so use -h localhost, not its IP.
-- Back up first:
--   pg_dump -h localhost -U kc_user -d kc_db -Fc -f ~/kc_db_backup_YYYYMMDD.dump
-- Apply:
--   psql -h localhost -U kc_user -d kc_db -f ~/add_season_stats_snapshot.sql

ALTER TABLE "Seasons"
    ADD COLUMN IF NOT EXISTS "StatsSnapshot" JSONB NULL;
