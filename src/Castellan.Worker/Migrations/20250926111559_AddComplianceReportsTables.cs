using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Castellan.Worker.Migrations
{
    /// <inheritdoc />
    public partial class AddComplianceReportsTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrelationContext",
                table: "SecurityEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationIds",
                table: "SecurityEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ComplianceControls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Framework = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ControlId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ControlName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Priority = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ValidationQuery = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceControls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceReports",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Framework = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ReportType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Generated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GeneratedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ImplementationPercentage = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalControls = table.Column<int>(type: "INTEGER", nullable: false),
                    ImplementedControls = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedControls = table.Column<int>(type: "INTEGER", nullable: false),
                    GapCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RiskScore = table.Column<float>(type: "REAL", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    KeyFindings = table.Column<string>(type: "text", nullable: true),
                    Recommendations = table.Column<string>(type: "text", nullable: true),
                    NextReview = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReportData = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceReports", x => x.Id);
                });

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

            migrationBuilder.CreateTable(
                name: "ThreatScanHistory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ScanType = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Duration = table.Column<double>(type: "REAL", nullable: false),
                    FilesScanned = table.Column<int>(type: "INTEGER", nullable: false),
                    DirectoriesScanned = table.Column<int>(type: "INTEGER", nullable: false),
                    BytesScanned = table.Column<long>(type: "INTEGER", nullable: false),
                    ThreatsFound = table.Column<int>(type: "INTEGER", nullable: false),
                    MalwareDetected = table.Column<int>(type: "INTEGER", nullable: false),
                    BackdoorsDetected = table.Column<int>(type: "INTEGER", nullable: false),
                    SuspiciousFiles = table.Column<int>(type: "INTEGER", nullable: false),
                    RiskLevel = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    ScanPath = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThreatScanHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceAssessmentResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReportId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    ControlId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Framework = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    Evidence = table.Column<string>(type: "text", nullable: true),
                    Findings = table.Column<string>(type: "text", nullable: true),
                    Recommendations = table.Column<string>(type: "text", nullable: true),
                    AssessedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceAssessmentResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComplianceAssessmentResults_ComplianceReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "ComplianceReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAssessmentResults_ReportControl",
                table: "ComplianceAssessmentResults",
                columns: new[] { "ReportId", "ControlId" });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceAssessmentResults_Status",
                table: "ComplianceAssessmentResults",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceControls_FrameworkControlId",
                table: "ComplianceControls",
                columns: new[] { "Framework", "ControlId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceControls_IsActive",
                table: "ComplianceControls",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceReports_CreatedDate",
                table: "ComplianceReports",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceReports_FrameworkDate",
                table: "ComplianceReports",
                columns: new[] { "Framework", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceReports_Status",
                table: "ComplianceReports",
                column: "Status");

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
                name: "ComplianceAssessmentResults");

            migrationBuilder.DropTable(
                name: "ComplianceControls");

            migrationBuilder.DropTable(
                name: "EventLogBookmarks");

            migrationBuilder.DropTable(
                name: "ThreatScanHistory");

            migrationBuilder.DropTable(
                name: "ComplianceReports");

            migrationBuilder.DropColumn(
                name: "CorrelationContext",
                table: "SecurityEvents");

            migrationBuilder.DropColumn(
                name: "CorrelationIds",
                table: "SecurityEvents");

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
                columns: new[] { "UpdatedAt", "Value" },
                values: new object[] { new DateTime(2025, 9, 15, 14, 56, 30, 777, DateTimeKind.Utc).AddTicks(7106), "2025-09-15" });

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 3,
                column: "UpdatedAt",
                value: new DateTime(2025, 9, 15, 14, 56, 30, 777, DateTimeKind.Utc).AddTicks(7108));
        }
    }
}
