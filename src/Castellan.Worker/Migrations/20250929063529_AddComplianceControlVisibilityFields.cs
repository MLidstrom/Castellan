using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Castellan.Worker.Migrations
{
    /// <inheritdoc />
    public partial class AddComplianceControlVisibilityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicableSectors",
                table: "ComplianceControls",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsUserVisible",
                table: "ComplianceControls",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Scope",
                table: "ComplianceControls",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 29, 6, 35, 28, 500, DateTimeKind.Utc).AddTicks(9824));

            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 29, 6, 35, 28, 500, DateTimeKind.Utc).AddTicks(9826));

            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 29, 6, 35, 28, 500, DateTimeKind.Utc).AddTicks(9828));

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 1,
                column: "UpdatedAt",
                value: new DateTime(2025, 9, 29, 6, 35, 28, 501, DateTimeKind.Utc).AddTicks(2));

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "UpdatedAt", "Value" },
                values: new object[] { new DateTime(2025, 9, 29, 6, 35, 28, 501, DateTimeKind.Utc).AddTicks(109), "2025-09-29" });

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 3,
                column: "UpdatedAt",
                value: new DateTime(2025, 9, 29, 6, 35, 28, 501, DateTimeKind.Utc).AddTicks(111));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicableSectors",
                table: "ComplianceControls");

            migrationBuilder.DropColumn(
                name: "IsUserVisible",
                table: "ComplianceControls");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "ComplianceControls");

            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 26, 11, 15, 59, 89, DateTimeKind.Utc).AddTicks(5759));

            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 26, 11, 15, 59, 89, DateTimeKind.Utc).AddTicks(5762));

            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 9, 26, 11, 15, 59, 89, DateTimeKind.Utc).AddTicks(5764));

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 1,
                column: "UpdatedAt",
                value: new DateTime(2025, 9, 26, 11, 15, 59, 89, DateTimeKind.Utc).AddTicks(5991));

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "UpdatedAt", "Value" },
                values: new object[] { new DateTime(2025, 9, 26, 11, 15, 59, 89, DateTimeKind.Utc).AddTicks(6100), "2025-09-26" });

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 3,
                column: "UpdatedAt",
                value: new DateTime(2025, 9, 26, 11, 15, 59, 89, DateTimeKind.Utc).AddTicks(6102));
        }
    }
}
