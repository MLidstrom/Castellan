using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Castellan.Worker.Migrations
{
    /// <inheritdoc />
    public partial class AddActionExecutionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActionExecutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConversationId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ChatMessageId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    ActionData = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    SuggestedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RolledBackAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExecutedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    RolledBackBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    BeforeState = table.Column<string>(type: "TEXT", nullable: true),
                    AfterState = table.Column<string>(type: "TEXT", nullable: true),
                    RollbackReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ExecutionLog = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionExecutions_ChatMessages_ChatMessageId",
                        column: x => x.ChatMessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActionExecutions_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 13, 14, 31, 535, DateTimeKind.Utc).AddTicks(994));

            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 13, 14, 31, 535, DateTimeKind.Utc).AddTicks(997));

            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 13, 14, 31, 535, DateTimeKind.Utc).AddTicks(999));

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 1,
                column: "UpdatedAt",
                value: new DateTime(2025, 10, 20, 13, 14, 31, 535, DateTimeKind.Utc).AddTicks(1315));

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "UpdatedAt", "Value" },
                values: new object[] { new DateTime(2025, 10, 20, 13, 14, 31, 535, DateTimeKind.Utc).AddTicks(1405), "2025-10-20" });

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 3,
                column: "UpdatedAt",
                value: new DateTime(2025, 10, 20, 13, 14, 31, 535, DateTimeKind.Utc).AddTicks(1407));

            migrationBuilder.CreateIndex(
                name: "IX_ActionExecutions_ChatMessageId",
                table: "ActionExecutions",
                column: "ChatMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionExecutions_ConversationId",
                table: "ActionExecutions",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionExecutions_ConversationStatus",
                table: "ActionExecutions",
                columns: new[] { "ConversationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ActionExecutions_ExecutedAt",
                table: "ActionExecutions",
                column: "ExecutedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ActionExecutions_Status",
                table: "ActionExecutions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ActionExecutions_StatusExecutedAt",
                table: "ActionExecutions",
                columns: new[] { "Status", "ExecutedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ActionExecutions_Type",
                table: "ActionExecutions",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionExecutions");

            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 16, 13, 32, 25, 196, DateTimeKind.Utc).AddTicks(6651));

            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 16, 13, 32, 25, 196, DateTimeKind.Utc).AddTicks(6654));

            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 16, 13, 32, 25, 196, DateTimeKind.Utc).AddTicks(6656));

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 1,
                column: "UpdatedAt",
                value: new DateTime(2025, 10, 16, 13, 32, 25, 196, DateTimeKind.Utc).AddTicks(6939));

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "UpdatedAt", "Value" },
                values: new object[] { new DateTime(2025, 10, 16, 13, 32, 25, 196, DateTimeKind.Utc).AddTicks(7133), "2025-10-16" });

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 3,
                column: "UpdatedAt",
                value: new DateTime(2025, 10, 16, 13, 32, 25, 196, DateTimeKind.Utc).AddTicks(7135));
        }
    }
}
