using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpaceOS.Modules.Procurement.Infrastructure.Migrations;

public partial class AddSupplierComplaint : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. Create ProcurementSequences table for order number generation
        migrationBuilder.CreateTable(
            name: "ProcurementSequences",
            schema: "spaceos_procurement",
            columns: table => new
            {
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                SequenceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Year = table.Column<int>(type: "integer", nullable: false),
                LastValue = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
            },
            constraints: table => table.PrimaryKey("PK_ProcurementSequences", x => new { x.TenantId, x.SequenceType, x.Year }));

        // 2. Update Deliveries table to add QualityInspection owned entity columns
        migrationBuilder.AddColumn<short>(
            name: "QualityStatus",
            schema: "spaceos_procurement",
            table: "Deliveries",
            type: "smallint",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "AcceptedQuantity",
            schema: "spaceos_procurement",
            table: "Deliveries",
            type: "numeric(10,2)",
            precision: 10,
            scale: 2,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "RejectedQuantity",
            schema: "spaceos_procurement",
            table: "Deliveries",
            type: "numeric(10,2)",
            precision: 10,
            scale: 2,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DefectDescription",
            schema: "spaceos_procurement",
            table: "Deliveries",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DefectPhotoPaths",
            schema: "spaceos_procurement",
            table: "Deliveries",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "InspectedAt",
            schema: "spaceos_procurement",
            table: "Deliveries",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "InspectedBy",
            schema: "spaceos_procurement",
            table: "Deliveries",
            type: "character varying(255)",
            maxLength: 255,
            nullable: true);

        // 3. Create SupplierComplaints table
        migrationBuilder.CreateTable(
            name: "SupplierComplaints",
            schema: "spaceos_procurement",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                ComplaintNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                DeliveryId = table.Column<Guid>(type: "uuid", nullable: false),
                SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                ComplaintType = table.Column<short>(type: "smallint", nullable: false),
                Status = table.Column<short>(type: "smallint", nullable: false),
                Description = table.Column<string>(type: "text", nullable: false),
                RequestedAction = table.Column<short>(type: "smallint", nullable: false),
                CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                WithdrawnAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                WithdrawalReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),

                // ComplaintResponse owned entity (nullable)
                ResponseType = table.Column<short>(type: "smallint", nullable: true),
                ResponseText = table.Column<string>(type: "text", nullable: true),
                ProposedAction = table.Column<short>(type: "smallint", nullable: true),
                ProposedValue = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                ProposedValueCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                ResponseProvidedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                ResponseProvidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),

                // ComplaintResolution owned entity (nullable)
                ResolutionType = table.Column<short>(type: "smallint", nullable: true),
                ResolutionAction = table.Column<short>(type: "smallint", nullable: true),
                ResolutionValue = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                ResolutionValueCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                ResolutionNotes = table.Column<string>(type: "text", nullable: true),
                ResolvedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_SupplierComplaints", x => x.Id));

        // 4. Create indexes
        migrationBuilder.CreateIndex(
            name: "IX_SupplierComplaints_TenantId_ComplaintNumber",
            schema: "spaceos_procurement",
            table: "SupplierComplaints",
            columns: new[] { "TenantId", "ComplaintNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SupplierComplaints_TenantId_Status",
            schema: "spaceos_procurement",
            table: "SupplierComplaints",
            columns: new[] { "TenantId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_SupplierComplaints_DeliveryId",
            schema: "spaceos_procurement",
            table: "SupplierComplaints",
            column: "DeliveryId");

        migrationBuilder.CreateIndex(
            name: "IX_SupplierComplaints_SupplierId",
            schema: "spaceos_procurement",
            table: "SupplierComplaints",
            column: "SupplierId");

        // 5. Add RLS policies for SupplierComplaints
        migrationBuilder.Sql(@"
ALTER TABLE spaceos_procurement.""SupplierComplaints"" ENABLE ROW LEVEL SECURITY;
ALTER TABLE spaceos_procurement.""SupplierComplaints"" FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON spaceos_procurement.""SupplierComplaints""
    USING (""TenantId"" = current_setting('app.tenant_id')::uuid);
");

        // 6. Create complaint number sequence function
        migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_next_complaint_number(p_tenant_id uuid, p_year int)
RETURNS text AS $$
DECLARE
    v_seq int;
BEGIN
    -- Advisory lock to prevent concurrent sequence generation
    PERFORM pg_advisory_xact_lock(hashtext(p_tenant_id::text || 'complaint' || p_year::text));

    -- Insert or update the sequence
    INSERT INTO spaceos_procurement.""ProcurementSequences"" (""TenantId"", ""SequenceType"", ""Year"", ""LastValue"")
    VALUES (p_tenant_id, 'complaint', p_year, 1)
    ON CONFLICT (""TenantId"", ""SequenceType"", ""Year"")
    DO UPDATE SET ""LastValue"" = spaceos_procurement.""ProcurementSequences"".""LastValue"" + 1
    RETURNING ""LastValue"" INTO v_seq;

    -- Return formatted complaint number: CMP-YYYY-NNNNN
    RETURN 'CMP-' || p_year::text || '-' || lpad(v_seq::text, 5, '0');
END;
$$ LANGUAGE plpgsql;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Drop function
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_next_complaint_number(uuid, int);");

        // Drop SupplierComplaints table
        migrationBuilder.DropTable(
            name: "SupplierComplaints",
            schema: "spaceos_procurement");

        // Drop ProcurementSequences table
        migrationBuilder.DropTable(
            name: "ProcurementSequences",
            schema: "spaceos_procurement");

        // Remove QualityInspection columns from Deliveries
        migrationBuilder.DropColumn(name: "QualityStatus", schema: "spaceos_procurement", table: "Deliveries");
        migrationBuilder.DropColumn(name: "AcceptedQuantity", schema: "spaceos_procurement", table: "Deliveries");
        migrationBuilder.DropColumn(name: "RejectedQuantity", schema: "spaceos_procurement", table: "Deliveries");
        migrationBuilder.DropColumn(name: "DefectDescription", schema: "spaceos_procurement", table: "Deliveries");
        migrationBuilder.DropColumn(name: "DefectPhotoPaths", schema: "spaceos_procurement", table: "Deliveries");
        migrationBuilder.DropColumn(name: "InspectedAt", schema: "spaceos_procurement", table: "Deliveries");
        migrationBuilder.DropColumn(name: "InspectedBy", schema: "spaceos_procurement", table: "Deliveries");
    }
}
