-- Clear the "Hopeful" tag from log entries where it is the only tag.
--
-- Most of these were never chosen on purpose: "Hopeful" ended up as the lone
-- tag on dozens of entries that should have been left untagged. Entries that
-- carry "Hopeful" alongside other tags were tagged deliberately, so they are
-- left alone. The few lone-Hopeful entries that were intentional go too; they
-- can be re-tagged by hand.
--
-- Run on mrtn-psql (10.0.0.225) against kc_db.

BEGIN;

DELETE FROM "LogEntryTags" lt
USING "Tags" t
WHERE lt."TagId" = t."TagId"
  AND t."Name" = 'Hopeful'
  AND lt."LogId" IN (
      SELECT "LogId"
      FROM "LogEntryTags"
      GROUP BY "LogId"
      HAVING COUNT(*) = 1
  );

COMMIT;
