using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Castellan.Worker.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 15, 14, 56, 30, 777, DateTimeKind.Utc).AddTicks(6578));

            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 15, 14, 56, 30, 777, DateTimeKind.Utc).AddTicks(6581));

            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 15, 14, 56, 30, 777, DateTimeKind.Utc).AddTicks(6583));

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 1,
                column: "UpdatedAt",
                value: new DateTime(2025, 9, 15, 14, 56, 30, 777, DateTimeKind.Utc).AddTicks(6871));

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 2,
                column: "UpdatedAt",
                value: new DateTime(2025, 9, 15, 14, 56, 30, 777, DateTimeKind.Utc).AddTicks(7106));

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 3,
                column: "UpdatedAt",
                value: new DateTime(2025, 9, 15, 14, 56, 30, 777, DateTimeKind.Utc).AddTicks(7108));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 15, 13, 49, 25, 518, DateTimeKind.Utc).AddTicks(8909));

            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 15, 13, 49, 25, 518, DateTimeKind.Utc).AddTicks(8912));

            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 15, 13, 49, 25, 518, DateTimeKind.Utc).AddTicks(8914));

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 1,
                column: "UpdatedAt",
                value: new DateTime(2025, 9, 15, 13, 49, 25, 518, DateTimeKind.Utc).AddTicks(9091));

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 2,
                column: "UpdatedAt",
                value: new DateTime(2025, 9, 15, 13, 49, 25, 518, DateTimeKind.Utc).AddTicks(9201));

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 3,
                column: "UpdatedAt",
                value: new DateTime(2025, 9, 15, 13, 49, 25, 518, DateTimeKind.Utc).AddTicks(9202));
        }
    }
}
