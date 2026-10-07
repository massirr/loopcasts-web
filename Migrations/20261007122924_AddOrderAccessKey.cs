using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoopCastsWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderAccessKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessKey",
                table: "Orders",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // Existing orders get a random key too (32 hex characters, like new orders)
            migrationBuilder.Sql("UPDATE Orders SET AccessKey = upper(hex(randomblob(16))) WHERE AccessKey = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessKey",
                table: "Orders");
        }
    }
}
