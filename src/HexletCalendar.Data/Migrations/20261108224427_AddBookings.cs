using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HexletCalendar.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookings : Migration
    {
        private static readonly string[] IntervalColumns = ["StartAt", "EndAt"];
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    EventTypeName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StartAt = table.Column<long>(type: "INTEGER", nullable: false),
                    EndAt = table.Column<long>(type: "INTEGER", nullable: false),
                    GuestName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GuestEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.Id);
                    table.CheckConstraint("CK_Bookings_Interval", "EndAt > StartAt");
                    table.ForeignKey(
                        name: "FK_Bookings_EventTypes_EventTypeId",
                        column: x => x.EventTypeId,
                        principalTable: "EventTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_EventTypeId",
                table: "Bookings",
                column: "EventTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_IdempotencyKey",
                table: "Bookings",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_StartAt_EndAt",
                table: "Bookings",
                columns: IntervalColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);
            migrationBuilder.DropTable(
                name: "Bookings");
        }
    }
}
