-- PR-M7: procurement_outbox + procurement_inbox
-- ADR-039 outbox pattern (OPEN-11: SchemaVersion for payload evolution)
-- Inbox pattern (OPEN-02): ON CONFLICT DO NOTHING for idempotent receivers
-- DB-P-10: retention indexes

CREATE TABLE IF NOT EXISTS spaceos_procurement.procurement_outbox (
    "Id"             uuid          PRIMARY KEY,
    "TenantId"       uuid          NOT NULL,
    "MessageType"    varchar(64)   NOT NULL,
    "SchemaVersion"  int           NOT NULL DEFAULT 1,
    "IdempotencyKey" uuid          NOT NULL,
    "PayloadJson"    jsonb         NOT NULL
        CHECK (octet_length("PayloadJson"::text) <= 65536),
    "Status"         varchar(20)   NOT NULL
        CHECK ("Status" IN ('Pending','InFlight','Completed','Failed')),
    "AttemptCount"   int           NOT NULL DEFAULT 0,
    "NextAttemptAt"  timestamptz   NOT NULL DEFAULT now(),
    "LeaseUntil"     timestamptz   NULL,
    "LastError"      varchar(2000) NULL,
    "CreatedAt"      timestamptz   NOT NULL DEFAULT now(),
    "ProcessedAt"    timestamptz   NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_Outbox_Tenant_Type_IdemKey"
    ON spaceos_procurement.procurement_outbox ("TenantId","MessageType","IdempotencyKey");

-- BE-P-03: claim index (Pending + InFlight with expired lease)
CREATE INDEX IF NOT EXISTS "IX_Outbox_Claim"
    ON spaceos_procurement.procurement_outbox ("Status","NextAttemptAt")
    WHERE "Status" IN ('Pending','InFlight');

-- DB-P-10: retention sweep index
CREATE INDEX IF NOT EXISTS "IX_Outbox_Completed"
    ON spaceos_procurement.procurement_outbox ("ProcessedAt")
    WHERE "Status" = 'Completed';

-- RLS (worker BYPASSRLS, but app role needs RLS for enqueue)
ALTER TABLE spaceos_procurement.procurement_outbox ENABLE ROW LEVEL SECURITY;
ALTER TABLE spaceos_procurement.procurement_outbox FORCE ROW LEVEL SECURITY;

DO $$ BEGIN
  IF NOT EXISTS (
    SELECT FROM pg_policies
    WHERE tablename = 'procurement_outbox'
      AND schemaname = 'spaceos_procurement'
      AND policyname = 'rls_procurement_outbox_tenant'
  ) THEN
    CREATE POLICY rls_procurement_outbox_tenant
        ON spaceos_procurement.procurement_outbox
        USING ("TenantId" = current_setting('app.tenant_id')::uuid);
  END IF;
END $$;

-- Inbox: bejövő receiver dedup-tárolás (OPEN-02)
CREATE TABLE IF NOT EXISTS spaceos_procurement.procurement_inbox (
    "TenantId"       uuid        NOT NULL,
    "MessageType"    varchar(64) NOT NULL,
    "IdempotencyKey" uuid        NOT NULL,
    "ProcessedAt"    timestamptz NOT NULL DEFAULT now(),
    "ResultRef"      uuid        NULL,
    PRIMARY KEY ("TenantId","MessageType","IdempotencyKey")
);

-- DB-P-10: retention sweep
CREATE INDEX IF NOT EXISTS "IX_Inbox_ProcessedAt"
    ON spaceos_procurement.procurement_inbox ("ProcessedAt");

-- RLS
ALTER TABLE spaceos_procurement.procurement_inbox ENABLE ROW LEVEL SECURITY;
ALTER TABLE spaceos_procurement.procurement_inbox FORCE ROW LEVEL SECURITY;

DO $$ BEGIN
  IF NOT EXISTS (
    SELECT FROM pg_policies
    WHERE tablename = 'procurement_inbox'
      AND schemaname = 'spaceos_procurement'
      AND policyname = 'rls_procurement_inbox_tenant'
  ) THEN
    CREATE POLICY rls_procurement_inbox_tenant
        ON spaceos_procurement.procurement_inbox
        USING ("TenantId" = current_setting('app.tenant_id')::uuid);
  END IF;
END $$;

-- Worker grants (applied after worker role exists from PR-M1)
DO $$ BEGIN
  IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'spaceos_procurement_worker') THEN
    GRANT SELECT, UPDATE ON spaceos_procurement.procurement_outbox TO spaceos_procurement_worker;
    GRANT INSERT ON spaceos_procurement.procurement_audit_log TO spaceos_procurement_worker;
  END IF;
END $$;
