-- Every LogEntry now counts toward progress, so the per-entry flag distinguishing
-- "contributing" entries from others no longer has a purpose. Drop it.
ALTER TABLE "LogEntries" DROP COLUMN "ContributesToProgress";
