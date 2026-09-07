-- LogEntries had no Title column — entries were identified only by their content
-- preview. The redesigned frontend lists/edits entries by title, so add it as
-- optional (existing rows have no title and shouldn't be forced to backfill one).
ALTER TABLE "LogEntries" ADD COLUMN "Title" VARCHAR(200);
