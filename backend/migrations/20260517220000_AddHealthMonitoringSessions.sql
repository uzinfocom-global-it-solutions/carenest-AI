-- Migration: AddHealthMonitoringSessions
-- Run with: psql -U postgres -d carenestai -f 20260517220000_AddHealthMonitoringSessions.sql

BEGIN;

CREATE TABLE IF NOT EXISTS "HealthMonitoringSessions" (
    "Id"                          SERIAL PRIMARY KEY,
    "FamilyId"                    INTEGER NOT NULL REFERENCES "Families"("Id") ON DELETE CASCADE,
    "ChildId"                     INTEGER REFERENCES "Children"("Id") ON DELETE SET NULL,
    "IssueType"                   VARCHAR(50) NOT NULL,
    "Severity"                    VARCHAR(20) NOT NULL DEFAULT 'Low',
    "Status"                      VARCHAR(20) NOT NULL DEFAULT 'Active',
    "EscalationLevel"             VARCHAR(20) NOT NULL DEFAULT 'None',
    "RiskScore"                   INTEGER NOT NULL DEFAULT 0,
    "StartedAt"                   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "LastUpdatedAt"               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "LastInteractionAt"           TIMESTAMPTZ,
    "NextFollowUpAt"              TIMESTAMPTZ,
    "MonitoringPlanJson"          JSONB,
    "InitialSymptomDescription"   VARCHAR(1000),
    "LastUserResponse"            VARCHAR(1000),
    "FollowUpCount"               INTEGER NOT NULL DEFAULT 0,
    "MissedFollowUps"             INTEGER NOT NULL DEFAULT 0,
    "ResolutionSummary"           VARCHAR(2000),
    "IsResolved"                  BOOLEAN NOT NULL DEFAULT FALSE,
    "ResolvedAt"                  TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS "IX_HealthMonitoringSessions_FamilyId"
    ON "HealthMonitoringSessions" ("FamilyId");

CREATE INDEX IF NOT EXISTS "IX_HealthMonitoringSessions_FamilyId_IsResolved"
    ON "HealthMonitoringSessions" ("FamilyId", "IsResolved");

CREATE INDEX IF NOT EXISTS "IX_HealthMonitoringSessions_FamilyId_IssueType_IsResolved"
    ON "HealthMonitoringSessions" ("FamilyId", "IssueType", "IsResolved");

CREATE INDEX IF NOT EXISTS "IX_HealthMonitoringSessions_NextFollowUpAt"
    ON "HealthMonitoringSessions" ("NextFollowUpAt")
    WHERE "IsResolved" = FALSE AND "NextFollowUpAt" IS NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260517220000_AddHealthMonitoringSessions', '9.0.0')
ON CONFLICT DO NOTHING;

COMMIT;
