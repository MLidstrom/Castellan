using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Castellan.Worker.Migrations
{
    /// <inheritdoc />
    public partial class FixActionExecutionIdAutoIncrement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 21, 11, 39, 24, 684, DateTimeKind.Utc).AddTicks(7706));

            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 21, 11, 39, 24, 684, DateTimeKind.Utc).AddTicks(7709));

            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 21, 11, 39, 24, 684, DateTimeKind.Utc).AddTicks(7711));

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 1,
                column: "UpdatedAt",
                value: new DateTime(2025, 10, 21, 11, 39, 24, 684, DateTimeKind.Utc).AddTicks(8056));

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "UpdatedAt", "Value" },
                values: new object[] { new DateTime(2025, 10, 21, 11, 39, 24, 684, DateTimeKind.Utc).AddTicks(8153), "2025-10-21" });

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 3,
                column: "UpdatedAt",
                value: new DateTime(2025, 10, 21, 11, 39, 24, 684, DateTimeKind.Utc).AddTicks(8154));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
