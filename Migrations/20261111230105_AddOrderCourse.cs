using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoopCastsWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderCourse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CourseId",
                table: "Orders",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CourseId",
                table: "Orders",
                column: "CourseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Courses_CourseId",
                table: "Orders",
                column: "CourseId",
                principalTable: "Courses",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Courses_CourseId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_CourseId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CourseId",
                table: "Orders");
        }
    }
}
