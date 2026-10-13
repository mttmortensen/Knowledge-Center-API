-- Learn entries can record the Log that was made from them ("save as log" in the
-- web app). Learn still never creates or edits logs; it only stores the id.
-- Additive and safe to re-run.
--
-- Back up first (from MRTN-LAPPS):
--   pg_dump -h 10.0.0.225 -U kc_user -d kc_db -Fc -f ~/kc_db_backup_YYYYMMDD.dump
-- Apply:
--   psql -h 10.0.0.225 -U kc_user -d kc_db -f SQL/add_learn_entry_log_link.sql

-- SET NULL so deleting a log (or the node / domain that owns it) just unlinks
-- the entry instead of being blocked by it.
ALTER TABLE "LearnEntries"
    ADD COLUMN IF NOT EXISTS "LogId" INTEGER NULL
        CONSTRAINT "FK_LearnEntries_LogEntries" REFERENCES "LogEntries" ("LogId") ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS "IX_LearnEntries_LogId" ON "LearnEntries" ("LogId");
