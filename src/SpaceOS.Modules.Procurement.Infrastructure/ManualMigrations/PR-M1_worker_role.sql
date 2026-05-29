-- PR-M1: spaceos_procurement_worker role (BYPASSRLS — ADR-024)
-- Idempotent: DO $$ IF NOT EXISTS $$
DO $$ BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'spaceos_procurement_worker') THEN
    CREATE ROLE spaceos_procurement_worker LOGIN BYPASSRLS;
  END IF;
END $$;

-- Narrow grants to worker role (only what worker needs)
-- Applied after PR-M6/M7 tables exist (run after PR-M6, PR-M7)
-- GRANT SELECT, UPDATE ON spaceos_procurement.procurement_outbox TO spaceos_procurement_worker;
-- GRANT SELECT, UPDATE ON spaceos_procurement.delivery_line TO spaceos_procurement_worker; -- InventorySyncStatus
-- GRANT INSERT ON spaceos_procurement.purchase_requisition TO spaceos_procurement_worker; -- from-reorder-alert worker path
-- GRANT INSERT ON spaceos_procurement.procurement_audit_log TO spaceos_procurement_worker;
