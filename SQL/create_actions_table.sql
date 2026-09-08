CREATE TABLE "Actions" (
    "Id" SERIAL PRIMARY KEY,
    "KnowledgeNodeId" INT NOT NULL REFERENCES "KnowledgeNodes"("Id"),
    "ActionText" VARCHAR(500) NOT NULL,
    "Status" VARCHAR(20) NOT NULL DEFAULT 'Open',
    "CreatedAt" TIMESTAMP NOT NULL,
    "CompletedAt" TIMESTAMP NULL
);

CREATE INDEX "IX_Actions_KnowledgeNodeId" ON "Actions" ("KnowledgeNodeId");
