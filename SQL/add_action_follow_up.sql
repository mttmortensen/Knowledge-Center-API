-- A short note on what happened after an action was closed. Optional, and it
-- stays on the row if the action is reopened so the history isn't lost.
ALTER TABLE "Actions" ADD COLUMN "FollowUp" VARCHAR(2000) NULL;
