-- PR-M4: invoice_match (append-only audit snapshot)
-- DB-P-02: fn_prevent_invoice_match_mutation trigger + REVOKE (dual protection)

CREATE TABLE IF NOT EXISTS spaceos_procurement.invoice_match (
    "Id"                    uuid          PRIMARY KEY,
    "TenantId"              uuid          NOT NULL,
    "InvoiceId"             uuid          NOT NULL
        REFERENCES spaceos_procurement.supplier_invoice("Id") ON DELETE RESTRICT,
    "PurchaseOrderId"       uuid          NOT NULL,
    "Outcome"               varchar(20)   NOT NULL
        CHECK ("Outcome" IN ('Matched','Exception')),
    "LineDetailJson"        jsonb         NOT NULL
        CHECK (octet_length("LineDetailJson"::text) <= 65536),
    "VarianceSummary"       varchar(2000) NOT NULL,
    "PriceTolerancePct"     numeric(6,4)  NOT NULL,
    "QuantityToleranceAbs"  int           NOT NULL,
    "EvaluatedAt"           timestamptz   NOT NULL DEFAULT now(),
    -- DB-P-03: composite FK
    FOREIGN KEY ("InvoiceId","TenantId")
        REFERENCES spaceos_procurement.supplier_invoice("Id","TenantId")
        ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS "IX_Match_Invoice"
    ON spaceos_procurement.invoice_match ("InvoiceId");

-- DB-P-02: append-only enforcement (trigger)
CREATE OR REPLACE FUNCTION spaceos_procurement.fn_prevent_invoice_match_mutation()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'invoice_match is append-only (no UPDATE/DELETE allowed)';
END $$;

DROP TRIGGER IF EXISTS trg_invoice_match_immutable ON spaceos_procurement.invoice_match;
CREATE TRIGGER trg_invoice_match_immutable
    BEFORE UPDATE OR DELETE ON spaceos_procurement.invoice_match
    FOR EACH ROW EXECUTE FUNCTION spaceos_procurement.fn_prevent_invoice_match_mutation();

-- RLS
ALTER TABLE spaceos_procurement.invoice_match ENABLE ROW LEVEL SECURITY;
ALTER TABLE spaceos_procurement.invoice_match FORCE ROW LEVEL SECURITY;

DO $$ BEGIN
  IF NOT EXISTS (
    SELECT FROM pg_policies
    WHERE tablename = 'invoice_match'
      AND schemaname = 'spaceos_procurement'
      AND policyname = 'rls_invoice_match_tenant'
  ) THEN
    CREATE POLICY rls_invoice_match_tenant
        ON spaceos_procurement.invoice_match
        USING ("TenantId" = current_setting('app.tenant_id')::uuid);
  END IF;
END $$;

-- DB-P-02: dual protection — revoke UPDATE/DELETE from app role
-- (Run after confirming spaceos_procurement_app role exists)
DO $$ BEGIN
  IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'spaceos_procurement_app') THEN
    REVOKE UPDATE, DELETE ON spaceos_procurement.invoice_match FROM spaceos_procurement_app;
  END IF;
END $$;
