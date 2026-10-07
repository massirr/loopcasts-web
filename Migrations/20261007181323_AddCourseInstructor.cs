using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoopCastsWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseInstructor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Instructor",
                table: "Courses",
                type: "TEXT",
                nullable: false,
                defaultValue: "Marieke De Smet");   // existing courses: Marieke records most courses (handbook ch. 1)
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Instructor",
                table: "Courses");
        }
    }
}
