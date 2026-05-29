-- PR-M3: supplier_invoice + supplier_invoice_line
-- DB-P-01: NO user "xmin" column
-- DB-P-03: composite FK for line tenant-integrity
-- DB-P-04/05: lookup and status indexes
-- DB-P-11: SupplierInvoiceNumber stored NORMALIZED (UPPER+trim in domain)

CREATE TABLE IF NOT EXISTS spaceos_procurement.supplier_invoice (
    "Id"                    uuid          PRIMARY KEY,
    "TenantId"              uuid          NOT NULL,
    "SupplierId"            uuid          NOT NULL,
    "PurchaseOrderId"       uuid          NOT NULL,
    "SupplierInvoiceNumber" varchar(50)   NOT NULL,
    "InvoiceDate"           date          NOT NULL,
    "DueDate"               date          NULL,
    "Currency"              char(3)       NOT NULL CHECK ("Currency" ~ '^[A-Z]{3}$'),
    "Status"                varchar(20)   NOT NULL
        CHECK ("Status" IN ('Received','Matched','Exception','Approved','Disputed')),
    "TotalNetAmount"        numeric(18,4) NOT NULL,
    "TotalVatAmount"        numeric(18,4) NOT NULL,
    "TotalGrossAmount"      numeric(18,4) NOT NULL
        CHECK ("TotalGrossAmount" = "TotalNetAmount" + "TotalVatAmount"),
    "LatestMatchId"         uuid          NULL,
    "RecordedBy"            varchar(128)  NOT NULL,
    "VarianceApprovedBy"    varchar(128)  NULL,
    "DisputeReason"         varchar(2000) NULL,
    "CreatedAt"             timestamptz   NOT NULL DEFAULT now()
    -- DB-P-01: no user "xmin" column
);

-- DB-P-03: composite FK target
ALTER TABLE spaceos_procurement.supplier_invoice
    ADD CONSTRAINT IF NOT EXISTS "UQ_Invoice_Id_Tenant"
    UNIQUE ("Id","TenantId");

-- DB-P-11: UNIQUE on normalized SupplierInvoiceNumber (OPEN-06)
CREATE UNIQUE INDEX IF NOT EXISTS "UX_Invoice_Tenant_Supplier_Number"
    ON spaceos_procurement.supplier_invoice ("TenantId","SupplierId","SupplierInvoiceNumber");

-- DB-P-04: lookup indexes
CREATE INDEX IF NOT EXISTS "IX_Invoice_Tenant_Supplier"
    ON spaceos_procurement.supplier_invoice ("TenantId","SupplierId");
CREATE INDEX IF NOT EXISTS "IX_Invoice_Tenant_PO"
    ON spaceos_procurement.supplier_invoice ("TenantId","PurchaseOrderId");

-- DB-P-05: status index (active statuses)
CREATE INDEX IF NOT EXISTS "IX_Invoice_Tenant_Status"
    ON spaceos_procurement.supplier_invoice ("TenantId","Status")
    WHERE "Status" IN ('Received','Matched','Exception');

-- RLS
ALTER TABLE spaceos_procurement.supplier_invoice ENABLE ROW LEVEL SECURITY;
ALTER TABLE spaceos_procurement.supplier_invoice FORCE ROW LEVEL SECURITY;

DO $$ BEGIN
  IF NOT EXISTS (
    SELECT FROM pg_policies
    WHERE tablename = 'supplier_invoice'
      AND schemaname = 'spaceos_procurement'
      AND policyname = 'rls_supplier_invoice_tenant'
  ) THEN
    CREATE POLICY rls_supplier_invoice_tenant
        ON spaceos_procurement.supplier_invoice
        USING ("TenantId" = current_setting('app.tenant_id')::uuid);
  END IF;
END $$;

-- supplier_invoice_line
CREATE TABLE IF NOT EXISTS spaceos_procurement.supplier_invoice_line (
    "Id"                  uuid          PRIMARY KEY,
    "InvoiceId"           uuid          NOT NULL,
    "TenantId"            uuid          NOT NULL,
    "MaterialCode"        varchar(20)   NOT NULL,
    "PurchaseOrderLineId" uuid          NULL,
    "Quantity"            int           NOT NULL CHECK ("Quantity" > 0),
    "UnitPrice"           numeric(18,4) NOT NULL,
    "LineNetAmount"       numeric(18,4) NOT NULL,
    "LineVatAmount"       numeric(18,4) NOT NULL,
    "LineGrossAmount"     numeric(18,4) NOT NULL
        CHECK ("LineGrossAmount" = "LineNetAmount" + "LineVatAmount"),
    -- DB-P-03: composite FK guarantees line.TenantId == parent.TenantId
    FOREIGN KEY ("InvoiceId","TenantId")
        REFERENCES spaceos_procurement.supplier_invoice("Id","TenantId")
        ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS "IX_InvoiceLine_Invoice"
    ON spaceos_procurement.supplier_invoice_line ("InvoiceId");
-- DB-P-04: match-pairing index
CREATE INDEX IF NOT EXISTS "IX_InvoiceLine_POLine"
    ON spaceos_procurement.supplier_invoice_line ("PurchaseOrderLineId")
    WHERE "PurchaseOrderLineId" IS NOT NULL;

-- RLS on lines
ALTER TABLE spaceos_procurement.supplier_invoice_line ENABLE ROW LEVEL SECURITY;
ALTER TABLE spaceos_procurement.supplier_invoice_line FORCE ROW LEVEL SECURITY;

DO $$ BEGIN
  IF NOT EXISTS (
    SELECT FROM pg_policies
    WHERE tablename = 'supplier_invoice_line'
      AND schemaname = 'spaceos_procurement'
      AND policyname = 'rls_supplier_invoice_line_tenant'
  ) THEN
    CREATE POLICY rls_supplier_invoice_line_tenant
        ON spaceos_procurement.supplier_invoice_line
        USING ("TenantId" = current_setting('app.tenant_id')::uuid);
  END IF;
END $$;
