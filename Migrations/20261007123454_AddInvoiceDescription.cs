using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoopCastsWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Invoices",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // Existing invoices: copy the product name once (the names from Models/Plan.cs at this moment)
            migrationBuilder.Sql("""
                UPDATE Invoices SET Description = COALESCE(
                    (SELECT CASE o.PlanCode
                        WHEN 'monthly' THEN 'Monthly subscription'
                        WHEN 'yearly'  THEN 'Yearly subscription'
                        WHEN 'team'    THEN 'Team licence'
                        WHEN 'single'  THEN 'Single course'
                        ELSE o.PlanCode END
                     FROM Orders o WHERE o.Id = Invoices.OrderId), '')
                WHERE Description = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "Invoices");
        }
    }
}
