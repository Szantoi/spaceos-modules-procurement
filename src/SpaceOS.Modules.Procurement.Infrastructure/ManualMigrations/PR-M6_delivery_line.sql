-- PR-M6: delivery_line bővítés — InventorySyncStatus additív oszlop (OPEN-01, Döntés #3c)
-- Non-destructive: existing rows default to 'Pending'
-- Track G: outbox INSERT uses DeliveryId as idempotency-key (simplified — Delivery has no Lines in v1)

ALTER TABLE spaceos_procurement."Deliveries"
    ADD COLUMN IF NOT EXISTS "InventorySyncStatus" varchar(20) NOT NULL DEFAULT 'Pending'
    CHECK ("InventorySyncStatus" IN ('Pending','Synced','Failed'));

-- Index for worker query: claim pending deliveries needing sync
CREATE INDEX IF NOT EXISTS "IX_Deliveries_SyncStatus"
    ON spaceos_procurement."Deliveries" ("TenantId","InventorySyncStatus")
    WHERE "InventorySyncStatus" IN ('Pending','Failed');
