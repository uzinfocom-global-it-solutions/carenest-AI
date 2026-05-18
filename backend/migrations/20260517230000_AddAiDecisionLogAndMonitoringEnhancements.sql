-- Migration: AddAiDecisionLogAndMonitoringEnhancements
-- Adds: AiDecisionLogs table, new columns on HealthMonitoringSessions
-- Run ONCE against the carenestai database.

BEGIN;

-- ── 1. New columns on HealthMonitoringSessions ────────────────────────────
ALTER TABLE "HealthMonitoringSessions"
    ADD COLUMN IF NOT EXISTS "LifecyclePhase"       varchar(30)  NOT NULL DEFAULT 'Detected',
    ADD COLUMN IF NOT EXISTS "MonitoringChainId"    varchar(50)  NULL,
    ADD COLUMN IF NOT EXISTS "ResponseAnalysisJson" varchar(3000) NULL,
    ADD COLUMN IF NOT EXISTS "PredictedRiskScore"   integer      NULL,
    ADD COLUMN IF NOT EXISTS "PredictedEscalationAt" timestamptz NULL;

-- Partial index for follow-up dispatch queries
CREATE INDEX IF NOT EXISTS "IX_HealthMonitoringSessions_NextFollowUpAt_Active"
    ON "HealthMonitoringSessions" ("NextFollowUpAt")
    WHERE "IsResolved" = false AND "NextFollowUpAt" IS NOT NULL;

-- ── 2. Create AiDecisionLogs table ───────────────────────────────────────
CREATE TABLE IF NOT EXISTS "AiDecisionLogs" (
    "Id"                  serial      PRIMARY KEY,
    "FamilyId"            integer     NOT NULL REFERENCES "Families"("Id") ON DELETE CASCADE,
    "DecisionType"        varchar(50) NOT NULL,
    "Message"             varchar(2000) NOT NULL,
    "Priority"            varchar(20) NOT NULL DEFAULT 'Normal',
    "TargetChildId"       integer     NULL,
    "RelatedSessionId"    integer     NULL,
    "DecisionChainId"     varchar(50) NULL,
    "IdempotencyKey"      varchar(200) NULL,
    "Executed"            boolean     NOT NULL DEFAULT true,
    "SuppressedBy"        varchar(500) NULL,
    "ContextSnapshotJson" jsonb       NULL,
    "EscalationRationale" varchar(1000) NULL,
    "Metadata"            varchar(2000) NULL,
    "CreatedAt"           timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS "IX_AiDecisionLogs_FamilyId"
    ON "AiDecisionLogs" ("FamilyId");

CREATE INDEX IF NOT EXISTS "IX_AiDecisionLogs_FamilyId_DecisionType_CreatedAt"
    ON "AiDecisionLogs" ("FamilyId", "DecisionType", "CreatedAt");

CREATE INDEX IF NOT EXISTS "IX_AiDecisionLogs_DecisionChainId"
    ON "AiDecisionLogs" ("DecisionChainId")
    WHERE "DecisionChainId" IS NOT NULL;

-- Partial index for priority engine deduplication queries
CREATE INDEX IF NOT EXISTS "IX_AiDecisionLogs_Dedup"
    ON "AiDecisionLogs" ("FamilyId", "DecisionType", "TargetChildId", "RelatedSessionId", "CreatedAt")
    WHERE "Executed" = true AND "SuppressedBy" IS NULL;

-- ── 3. Record migration ───────────────────────────────────────────────────
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260517230000_AddAiDecisionLogAndMonitoringEnhancements', '9.0.0')
ON CONFLICT DO NOTHING;

COMMIT;
