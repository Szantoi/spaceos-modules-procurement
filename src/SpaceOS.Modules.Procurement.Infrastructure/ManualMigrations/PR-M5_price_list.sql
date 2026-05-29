-- PR-M5: price_list + price_list_entry + procurement_match_policy
-- DB-P-01: NO user "xmin"
-- DB-P-03: composite FK for entry tenant-integrity
-- DB-P-04/06/09: lookup, active, and overlap-guard indexes

CREATE TABLE IF NOT EXISTS spaceos_procurement.price_list (
    "Id"         uuid          PRIMARY KEY,
    "TenantId"   uuid          NOT NULL,
    "SupplierId" uuid          NOT NULL,
    "Currency"   char(3)       NOT NULL CHECK ("Currency" ~ '^[A-Z]{3}$'),
    "ValidFrom"  date          NOT NULL,
    "ValidTo"    date          NULL,
    "Status"     varchar(20)   NOT NULL
        CHECK ("Status" IN ('Draft','Active','Expired')),
    "CreatedAt"  timestamptz   NOT NULL DEFAULT now(),
    CHECK ("ValidTo" IS NULL OR "ValidTo" >= "ValidFrom")
    -- DB-P-01: no user "xmin" column
);

-- DB-P-03
ALTER TABLE spaceos_procurement.price_list
    ADD CONSTRAINT IF NOT EXISTS "UQ_PriceList_Id_Tenant"
    UNIQUE ("Id","TenantId");

-- DB-P-04
CREATE INDEX IF NOT EXISTS "IX_PriceList_Tenant_Supplier"
    ON spaceos_procurement.price_list ("TenantId","SupplierId");

-- DB-P-06: best-price query acceleration
CREATE INDEX IF NOT EXISTS "IX_PriceList_Active"
    ON spaceos_procurement.price_list ("TenantId","SupplierId","ValidFrom","ValidTo")
    WHERE "Status" = 'Active';

-- DB-P-09: overlap-guard (domain-enforced primary; this is DB-level net)
CREATE UNIQUE INDEX IF NOT EXISTS "UX_PriceList_Active_Validity"
    ON spaceos_procurement.price_list ("TenantId","SupplierId","Currency","ValidFrom")
    WHERE "Status" = 'Active';

-- RLS
ALTER TABLE spaceos_procurement.price_list ENABLE ROW LEVEL SECURITY;
ALTER TABLE spaceos_procurement.price_list FORCE ROW LEVEL SECURITY;

DO $$ BEGIN
  IF NOT EXISTS (
    SELECT FROM pg_policies
    WHERE tablename = 'price_list'
      AND schemaname = 'spaceos_procurement'
      AND policyname = 'rls_price_list_tenant'
  ) THEN
    CREATE POLICY rls_price_list_tenant
        ON spaceos_procurement.price_list
        USING ("TenantId" = current_setting('app.tenant_id')::uuid);
  END IF;
END $$;

-- price_list_entry
CREATE TABLE IF NOT EXISTS spaceos_procurement.price_list_entry (
    "Id"           uuid          PRIMARY KEY,
    "PriceListId"  uuid          NOT NULL,
    "TenantId"     uuid          NOT NULL,
    "MaterialCode" varchar(20)   NOT NULL,
    "UnitPrice"    numeric(18,4) NOT NULL CHECK ("UnitPrice" > 0),
    "MinQuantity"  int           NOT NULL DEFAULT 1 CHECK ("MinQuantity" >= 1),
    "MaxQuantity"  int           NULL CHECK ("MaxQuantity" IS NULL OR "MaxQuantity" >= "MinQuantity"),
    -- DB-P-03
    FOREIGN KEY ("PriceListId","TenantId")
        REFERENCES spaceos_procurement.price_list("Id","TenantId")
        ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS "IX_PriceEntry_PriceList"
    ON spaceos_procurement.price_list_entry ("PriceListId");
-- DB-P-06: best-price query
CREATE INDEX IF NOT EXISTS "IX_PriceEntry_Tenant_Material"
    ON spaceos_procurement.price_list_entry ("TenantId","MaterialCode");

-- RLS on entries
ALTER TABLE spaceos_procurement.price_list_entry ENABLE ROW LEVEL SECURITY;
ALTER TABLE spaceos_procurement.price_list_entry FORCE ROW LEVEL SECURITY;

DO $$ BEGIN
  IF NOT EXISTS (
    SELECT FROM pg_policies
    WHERE tablename = 'price_list_entry'
      AND schemaname = 'spaceos_procurement'
      AND policyname = 'rls_price_list_entry_tenant'
  ) THEN
    CREATE POLICY rls_price_list_entry_tenant
        ON spaceos_procurement.price_list_entry
        USING ("TenantId" = current_setting('app.tenant_id')::uuid);
  END IF;
END $$;

-- DB-P-12: procurement_match_policy (OPEN-05) — tenant-level tolerance override
CREATE TABLE IF NOT EXISTS spaceos_procurement.procurement_match_policy (
    "TenantId"             uuid          PRIMARY KEY,
    "PriceTolerancePct"    numeric(6,4)  NOT NULL DEFAULT 0.02
        CHECK ("PriceTolerancePct" >= 0),
    "QuantityToleranceAbs" int           NOT NULL DEFAULT 1
        CHECK ("QuantityToleranceAbs" >= 0),
    "CreatedAt"            timestamptz   NOT NULL DEFAULT now(),
    "UpdatedAt"            timestamptz   NOT NULL DEFAULT now()
);

-- RLS
ALTER TABLE spaceos_procurement.procurement_match_policy ENABLE ROW LEVEL SECURITY;
ALTER TABLE spaceos_procurement.procurement_match_policy FORCE ROW LEVEL SECURITY;

DO $$ BEGIN
  IF NOT EXISTS (
    SELECT FROM pg_policies
    WHERE tablename = 'procurement_match_policy'
      AND schemaname = 'spaceos_procurement'
      AND policyname = 'rls_procurement_match_policy_tenant'
  ) THEN
    CREATE POLICY rls_procurement_match_policy_tenant
        ON spaceos_procurement.procurement_match_policy
        USING ("TenantId" = current_setting('app.tenant_id')::uuid);
  END IF;
END $$;
