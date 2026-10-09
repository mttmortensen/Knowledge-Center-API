-- The Chat URL on a log entry went essentially unused, so the per-entry link
-- to an outside chat session no longer has a purpose. Drop it.
ALTER TABLE "LogEntries" DROP COLUMN "ChatURL";
