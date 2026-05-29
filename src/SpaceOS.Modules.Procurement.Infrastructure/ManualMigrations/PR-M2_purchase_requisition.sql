-- PR-M2: purchase_requisition + purchase_requisition_line
-- DB-P-01: NO user "xmin" column — EF maps to implicit system xmin
-- DB-P-03: composite FK targets for line tenant-integrity

CREATE TABLE IF NOT EXISTS spaceos_procurement.purchase_requisition (
    "Id"                       uuid         PRIMARY KEY,
    "TenantId"                 uuid         NOT NULL,
    "RequisitionNumber"        varchar(20)  NOT NULL,
    "Source"                   varchar(20)  NOT NULL
        CHECK ("Source" IN ('Manual','ReorderAlert')),
    "SourceReference"          uuid         NULL,
    "Status"                   varchar(20)  NOT NULL
        CHECK ("Status" IN ('Draft','Approved','ConvertedToPO','Rejected')),
    "RequestedBy"              varchar(128) NOT NULL,
    "ApprovedBy"               varchar(128) NULL,
    "ApprovedAt"               timestamptz  NULL,
    "RejectedReason"           varchar(2000) NULL,
    "ConvertedPurchaseOrderId" uuid         NULL,
    "Notes"                    varchar(2000) NULL,
    "CreatedAt"                timestamptz  NOT NULL DEFAULT now()
    -- DB-P-01: NINCS user "xmin" oszlop — az EF az implicit rendszer-xmin-re map-pel
);

-- DB-P-03: composite FK target for line rows
ALTER TABLE spaceos_procurement.purchase_requisition
    ADD CONSTRAINT IF NOT EXISTS "UQ_Requisition_Id_Tenant"
    UNIQUE ("Id","TenantId");

-- Idempotencia: per (TenantId, SourceReference) for ReorderAlert
CREATE UNIQUE INDEX IF NOT EXISTS "UX_Requisition_Tenant_SourceRef"
    ON spaceos_procurement.purchase_requisition ("TenantId","SourceReference")
    WHERE "Source" = 'ReorderAlert' AND "SourceReference" IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "UX_Requisition_Tenant_Number"
    ON spaceos_procurement.purchase_requisition ("TenantId","RequisitionNumber");

-- DB-P-05: status index (active statuses only)
CREATE INDEX IF NOT EXISTS "IX_Requisition_Tenant_Status"
    ON spaceos_procurement.purchase_requisition ("TenantId","Status")
    WHERE "Status" IN ('Draft','Approved');

-- RLS
ALTER TABLE spaceos_procurement.purchase_requisition ENABLE ROW LEVEL SECURITY;
ALTER TABLE spaceos_procurement.purchase_requisition FORCE ROW LEVEL SECURITY;

DO $$ BEGIN
  IF NOT EXISTS (
    SELECT FROM pg_policies
    WHERE tablename = 'purchase_requisition'
      AND schemaname = 'spaceos_procurement'
      AND policyname = 'rls_purchase_requisition_tenant'
  ) THEN
    CREATE POLICY rls_purchase_requisition_tenant
        ON spaceos_procurement.purchase_requisition
        USING ("TenantId" = current_setting('app.tenant_id')::uuid);
  END IF;
END $$;

-- purchase_requisition_line
CREATE TABLE IF NOT EXISTS spaceos_procurement.purchase_requisition_line (
    "Id"                  uuid         PRIMARY KEY,
    "RequisitionId"       uuid         NOT NULL,
    "TenantId"            uuid         NOT NULL,
    "MaterialCode"        varchar(20)  NOT NULL,
    "Quantity"            int          NOT NULL CHECK ("Quantity" > 0),
    "EstimatedUnitPrice"  numeric(18,4) NULL,
    "PreferredSupplierId" uuid         NULL,
    "Notes"               varchar(500) NULL,
    -- DB-P-03: composite FK guarantees line.TenantId == parent.TenantId
    FOREIGN KEY ("RequisitionId","TenantId")
        REFERENCES spaceos_procurement.purchase_requisition("Id","TenantId")
        ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS "IX_ReqLine_Requisition"
    ON spaceos_procurement.purchase_requisition_line ("RequisitionId");

-- RLS on lines
ALTER TABLE spaceos_procurement.purchase_requisition_line ENABLE ROW LEVEL SECURITY;
ALTER TABLE spaceos_procurement.purchase_requisition_line FORCE ROW LEVEL SECURITY;

DO $$ BEGIN
  IF NOT EXISTS (
    SELECT FROM pg_policies
    WHERE tablename = 'purchase_requisition_line'
      AND schemaname = 'spaceos_procurement'
      AND policyname = 'rls_purchase_requisition_line_tenant'
  ) THEN
    CREATE POLICY rls_purchase_requisition_line_tenant
        ON spaceos_procurement.purchase_requisition_line
        USING ("TenantId" = current_setting('app.tenant_id')::uuid);
  END IF;
END $$;

-- requisition_number_counters (OPEN-03) — per-tenant monotonic sequence
CREATE TABLE IF NOT EXISTS spaceos_procurement.requisition_number_counters (
    "TenantId"  uuid NOT NULL,
    "Year"      int  NOT NULL,
    "LastValue" int  NOT NULL DEFAULT 0,
    PRIMARY KEY ("TenantId","Year")
);

-- fn_next_requisition_number: advisory-lock-safe, Sales fn_next_quote_number precedent
CREATE OR REPLACE FUNCTION spaceos_procurement.fn_next_requisition_number(p_tenant uuid, p_year int)
RETURNS varchar LANGUAGE plpgsql AS $$
DECLARE
    v_next int;
BEGIN
    PERFORM pg_advisory_xact_lock(hashtext(p_tenant::text || p_year::text));
    INSERT INTO spaceos_procurement.requisition_number_counters("TenantId","Year","LastValue")
    VALUES (p_tenant, p_year, 1)
    ON CONFLICT ("TenantId","Year") DO UPDATE
        SET "LastValue" = spaceos_procurement.requisition_number_counters."LastValue" + 1
    RETURNING "LastValue" INTO v_next;
    RETURN 'PR-' || p_year::text || '-' || lpad(v_next::text, 5, '0');
END $$;
