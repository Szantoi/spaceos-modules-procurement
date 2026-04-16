using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpaceOS.Modules.Procurement.Infrastructure.Migrations;

public partial class InitialProcurementSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "spaceos_procurement");

        migrationBuilder.CreateTable(
            name: "Suppliers",
            schema: "spaceos_procurement",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ContactEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                LeadTimeDays = table.Column<int>(type: "integer", nullable: false),
                Rating = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Suppliers", x => x.Id));

        migrationBuilder.CreateTable(
            name: "PurchaseOrders",
            schema: "spaceos_procurement",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                MaterialType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Quantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                UnitPrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ExpectedDeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_PurchaseOrders", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Deliveries",
            schema: "spaceos_procurement",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                ReceivedQuantity = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                RecordedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Deliveries", x => x.Id));

        // Indexes
        migrationBuilder.CreateIndex("IX_Suppliers_TenantId_IsActive", "Suppliers", new[] { "TenantId", "IsActive" }, schema: "spaceos_procurement");
        migrationBuilder.CreateIndex("IX_PurchaseOrders_TenantId", "PurchaseOrders", "TenantId", schema: "spaceos_procurement");
        migrationBuilder.CreateIndex("IX_PurchaseOrders_TenantId_Status", "PurchaseOrders", new[] { "TenantId", "Status" }, schema: "spaceos_procurement");
        migrationBuilder.CreateIndex("IX_Deliveries_TenantId", "Deliveries", "TenantId", schema: "spaceos_procurement");
        migrationBuilder.CreateIndex("IX_Deliveries_PurchaseOrderId", "Deliveries", "PurchaseOrderId", schema: "spaceos_procurement");

        // RLS
        migrationBuilder.Sql(@"
ALTER TABLE spaceos_procurement.""Suppliers"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE spaceos_procurement.""Suppliers"" FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON spaceos_procurement.""Suppliers""
    USING (""TenantId"" = current_setting('app.current_tenant_id')::uuid);

ALTER TABLE spaceos_procurement.""PurchaseOrders"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE spaceos_procurement.""PurchaseOrders"" FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON spaceos_procurement.""PurchaseOrders""
    USING (""TenantId"" = current_setting('app.current_tenant_id')::uuid);

ALTER TABLE spaceos_procurement.""Deliveries"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE spaceos_procurement.""Deliveries"" FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON spaceos_procurement.""Deliveries""
    USING (""TenantId"" = current_setting('app.current_tenant_id')::uuid);
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DROP POLICY IF EXISTS tenant_isolation ON spaceos_procurement.""Suppliers"";
DROP POLICY IF EXISTS tenant_isolation ON spaceos_procurement.""PurchaseOrders"";
DROP POLICY IF EXISTS tenant_isolation ON spaceos_procurement.""Deliveries"";
");
        migrationBuilder.DropTable("Deliveries", "spaceos_procurement");
        migrationBuilder.DropTable("PurchaseOrders", "spaceos_procurement");
        migrationBuilder.DropTable("Suppliers", "spaceos_procurement");
    }
}
