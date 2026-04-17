using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpaceOS.Modules.Procurement.Infrastructure.Migrations;

public partial class AddSupplierCreatedAt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "ContactEmail",
            schema: "spaceos_procurement",
            table: "Suppliers",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(200)",
            oldMaxLength: 200);

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            schema: "spaceos_procurement",
            table: "Suppliers",
            type: "timestamp with time zone",
            nullable: false,
            defaultValueSql: "NOW()");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CreatedAt",
            schema: "spaceos_procurement",
            table: "Suppliers");

        migrationBuilder.AlterColumn<string>(
            name: "ContactEmail",
            schema: "spaceos_procurement",
            table: "Suppliers",
            type: "character varying(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "character varying(200)",
            oldMaxLength: 200,
            oldNullable: true);
    }
}
