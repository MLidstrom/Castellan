using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Castellan.Worker.Migrations
{
    /// <inheritdoc />
    public partial class AddEventLogBookmarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventLogBookmarks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChannelName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    BookmarkData = table.Column<byte[]>(type: "BLOB", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventLogBookmarks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventLogBookmarks_ChannelName",
                table: "EventLogBookmarks",
                column: "ChannelName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventLogBookmarks_UpdatedAt",
                table: "EventLogBookmarks",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventLogBookmarks");
        }
    }
}
