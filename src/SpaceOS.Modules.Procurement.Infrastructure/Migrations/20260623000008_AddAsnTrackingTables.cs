using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpaceOS.Modules.Procurement.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddAsnTrackingTables : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AsnShipments",
            schema: "spaceos_procurement",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                AsnNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                ExpectedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                QrPayload = table.Column<string>(type: "text", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                OfflineScannedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AsnShipments", x => x.Id);
                table.ForeignKey(
                    name: "FK_AsnShipments_PurchaseOrders_PurchaseOrderId",
                    column: x => x.PurchaseOrderId,
                    principalSchema: "spaceos_procurement",
                    principalTable: "PurchaseOrders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_AsnShipments_Suppliers_SupplierId",
                    column: x => x.SupplierId,
                    principalSchema: "spaceos_procurement",
                    principalTable: "Suppliers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ReceiptQueues",
            schema: "spaceos_procurement",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                AsnShipmentId = table.Column<Guid>(type: "uuid", nullable: false),
                ScannedBy = table.Column<Guid>(type: "uuid", nullable: false),
                ActualQuantity = table.Column<int>(type: "integer", nullable: false),
                ScannedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                SyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReceiptQueues", x => x.Id);
                table.ForeignKey(
                    name: "FK_ReceiptQueues_AsnShipments_AsnShipmentId",
                    column: x => x.AsnShipmentId,
                    principalSchema: "spaceos_procurement",
                    principalTable: "AsnShipments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AsnShipments_AsnNumber",
            schema: "spaceos_procurement",
            table: "AsnShipments",
            column: "AsnNumber",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AsnShipments_PurchaseOrderId_TenantId",
            schema: "spaceos_procurement",
            table: "AsnShipments",
            columns: new[] { "PurchaseOrderId", "TenantId" });

        migrationBuilder.CreateIndex(
            name: "IX_AsnShipments_Status_TenantId",
            schema: "spaceos_procurement",
            table: "AsnShipments",
            columns: new[] { "Status", "TenantId" });

        migrationBuilder.CreateIndex(
            name: "IX_AsnShipments_SupplierId",
            schema: "spaceos_procurement",
            table: "AsnShipments",
            column: "SupplierId");

        migrationBuilder.CreateIndex(
            name: "IX_ReceiptQueues_AsnShipmentId_TenantId",
            schema: "spaceos_procurement",
            table: "ReceiptQueues",
            columns: new[] { "AsnShipmentId", "TenantId" });

        migrationBuilder.CreateIndex(
            name: "IX_ReceiptQueues_Status_CreatedAt",
            schema: "spaceos_procurement",
            table: "ReceiptQueues",
            columns: new[] { "Status", "CreatedAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ReceiptQueues",
            schema: "spaceos_procurement");

        migrationBuilder.DropTable(
            name: "AsnShipments",
            schema: "spaceos_procurement");
    }
}
