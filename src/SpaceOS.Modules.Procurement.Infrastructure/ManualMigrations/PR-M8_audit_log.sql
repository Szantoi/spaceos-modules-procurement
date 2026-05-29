-- PR-M8: procurement_audit_log — append-only financial audit trail (SEC-P-05)
-- Same immutability pattern as invoice_match (DB-P-02)
-- Retention >= 7 years (accounting)

CREATE TABLE IF NOT EXISTS spaceos_procurement.procurement_audit_log (
    "Id"            uuid          PRIMARY KEY,
    "TenantId"      uuid          NOT NULL,
    "Actor"         varchar(128)  NOT NULL,
    "Action"        varchar(64)   NOT NULL,
    "AggregateType" varchar(48)   NOT NULL,
    "AggregateId"   uuid          NOT NULL,
    "BeforeJson"    jsonb         NULL,
    "AfterJson"     jsonb         NULL,
    "SourceIp"      inet          NULL,
    "CreatedAt"     timestamptz   NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS "IX_AuditLog_Tenant_Aggregate"
    ON spaceos_procurement.procurement_audit_log ("TenantId","AggregateId");
CREATE INDEX IF NOT EXISTS "IX_AuditLog_Tenant_CreatedAt"
    ON spaceos_procurement.procurement_audit_log ("TenantId","CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_AuditLog_Actor"
    ON spaceos_procurement.procurement_audit_log ("TenantId","Actor");

-- Append-only: trigger immutability (same as invoice_match)
CREATE OR REPLACE FUNCTION spaceos_procurement.fn_prevent_audit_log_mutation()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'procurement_audit_log is append-only (no UPDATE/DELETE allowed)';
END $$;

DROP TRIGGER IF EXISTS trg_audit_log_immutable ON spaceos_procurement.procurement_audit_log;
CREATE TRIGGER trg_audit_log_immutable
    BEFORE UPDATE OR DELETE ON spaceos_procurement.procurement_audit_log
    FOR EACH ROW EXECUTE FUNCTION spaceos_procurement.fn_prevent_audit_log_mutation();

-- RLS
ALTER TABLE spaceos_procurement.procurement_audit_log ENABLE ROW LEVEL SECURITY;
ALTER TABLE spaceos_procurement.procurement_audit_log FORCE ROW LEVEL SECURITY;

DO $$ BEGIN
  IF NOT EXISTS (
    SELECT FROM pg_policies
    WHERE tablename = 'procurement_audit_log'
      AND schemaname = 'spaceos_procurement'
      AND policyname = 'rls_procurement_audit_log_tenant'
  ) THEN
    CREATE POLICY rls_procurement_audit_log_tenant
        ON spaceos_procurement.procurement_audit_log
        USING ("TenantId" = current_setting('app.tenant_id')::uuid);
  END IF;
END $$;

-- Dual protection: revoke mutation from app role
DO $$ BEGIN
  IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'spaceos_procurement_app') THEN
    REVOKE UPDATE, DELETE ON spaceos_procurement.procurement_audit_log FROM spaceos_procurement_app;
  END IF;
END $$;

-- Worker grant for audit inserts
DO $$ BEGIN
  IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'spaceos_procurement_worker') THEN
    GRANT INSERT ON spaceos_procurement.procurement_audit_log TO spaceos_procurement_worker;
  END IF;
END $$;
