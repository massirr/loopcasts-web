using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoopCastsWeb.Migrations
{
    /// <inheritdoc />
    public partial class AccountRemindedFor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "RemindedFor",
                table: "Accounts",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RemindedFor",
                table: "Accounts");
        }
    }
}
