using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HexletCalendar.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialEventTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.CreateTable(
                name: "EventTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    DurationMinutes = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventTypes", x => x.Id);
                    table.CheckConstraint("CK_EventTypes_Description", "length(Description) BETWEEN 1 AND 2000");
                    table.CheckConstraint("CK_EventTypes_Duration", "DurationMinutes BETWEEN 15 AND 540 AND DurationMinutes % 15 = 0");
                    table.CheckConstraint("CK_EventTypes_Name", "length(Name) BETWEEN 1 AND 100");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.DropTable(
                name: "EventTypes");
        }
    }
}
