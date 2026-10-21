using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoopCastsWeb.Migrations
{
    /// <inheritdoc />
    public partial class VatCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VatCheckName",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VatCheckResult",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VatCheckedAt",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VatCheckName",
                table: "Invoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VatCheckResult",
                table: "Invoices",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VatCheckedAt",
                table: "Invoices",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VatCheckName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VatCheckResult",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VatCheckedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VatCheckName",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VatCheckResult",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VatCheckedAt",
                table: "Invoices");
        }
    }
}
