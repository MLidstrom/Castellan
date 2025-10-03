using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Castellan.Worker.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityEventRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComplianceAssessmentResults");

            migrationBuilder.DropTable(
                name: "ComplianceControls");

            migrationBuilder.DropTable(
                name: "ComplianceReports");

            migrationBuilder.CreateTable(
                name: "SecurityEventRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventId = table.Column<int>(type: "INTEGER", nullable: false),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RiskLevel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Confidence = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    MitreTechniques = table.Column<string>(type: "TEXT", nullable: false),
                    RecommendedActions = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Tags = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityEventRules", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 1, 9, 29, 29, 861, DateTimeKind.Utc).AddTicks(4584));

            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 1, 9, 29, 29, 861, DateTimeKind.Utc).AddTicks(4586));

            migrationBuilder.UpdateData(
                table: "MitreTechniques",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2025, 10, 1, 9, 29, 29, 861, DateTimeKind.Utc).AddTicks(4588));

            migrationBuilder.InsertData(
                table: "SecurityEventRules",
                columns: new[] { "Id", "Channel", "Confidence", "CreatedAt", "Description", "EventId", "EventType", "IsEnabled", "MitreTechniques", "ModifiedBy", "Priority", "RecommendedActions", "RiskLevel", "Summary", "Tags", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "Security", 85, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4624, "AuthenticationSuccess", true, "[\"T1078\"]", "System", 100, "[\"Monitor for unusual logon patterns\",\"Verify user identity\"]", "medium", "Successful logon", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "Security", 90, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4625, "AuthenticationFailure", true, "[\"T1110\"]", "System", 100, "[\"Investigate failed logon source\",\"Check for brute force attempts\",\"Review account lockout policies\"]", "high", "Failed logon attempt", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, "Security", 75, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4672, "PrivilegeEscalation", true, "[\"T1068\",\"T1078\"]", "System", 100, "[\"Investigate privilege assignment\",\"Verify administrative approval\",\"Monitor for abuse\"]", "medium", "Special privileges assigned to new logon", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, "Security", 80, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4688, "ProcessCreation", true, "[\"T1055\",\"T1059\"]", "System", 100, "[\"Review process parent-child relationships\",\"Check for suspicious command lines\"]", "medium", "Process creation", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, "Security", 90, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4720, "AccountManagement", true, "[\"T1136\"]", "System", 100, "[\"Verify account creation approval\",\"Review new account permissions\",\"Monitor for unauthorized accounts\"]", "high", "Account created", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 6, "Security", 90, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4722, "AccountManagement", true, "[\"T1078\"]", "System", 100, "[\"Verify account enablement approval\",\"Review account status changes\"]", "high", "Account enabled", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 7, "Security", 90, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4724, "AccountManagement", true, "[\"T1098\"]", "System", 100, "[\"Verify password reset approval\",\"Check for unauthorized password changes\"]", "high", "Account password reset", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 8, "Security", 95, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4728, "PrivilegeEscalation", true, "[\"T1068\",\"T1098\"]", "System", 100, "[\"Investigate group membership changes\",\"Verify administrative approval\",\"Review group permissions\"]", "critical", "Member added to security-enabled global group", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 9, "Security", 95, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4732, "PrivilegeEscalation", true, "[\"T1068\",\"T1098\"]", "System", 100, "[\"Investigate local group changes\",\"Verify administrative approval\",\"Monitor for privilege escalation\"]", "critical", "Member added to security-enabled local group", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 10, "Security", 85, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 7045, "ServiceInstallation", true, "[\"T1543\"]", "System", 100, "[\"Verify service installation approval\",\"Review service permissions\",\"Check for persistence mechanisms\"]", "high", "Service installed", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 11, "Security", 85, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4697, "ServiceInstallation", true, "[\"T1543\"]", "System", 100, "[\"Verify service installation approval\",\"Review service configuration\",\"Monitor for unauthorized services\"]", "high", "Service installed", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 12, "Security", 75, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4698, "ScheduledTask", true, "[\"T1053\"]", "System", 100, "[\"Review scheduled task configuration\",\"Verify task approval\",\"Monitor for persistence\"]", "medium", "Scheduled task created", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 13, "Security", 75, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4700, "ScheduledTask", true, "[\"T1053\"]", "System", 100, "[\"Review enabled scheduled tasks\",\"Verify task approval\"]", "medium", "Scheduled task enabled", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 14, "Security", 60, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 6005, "SystemStartup", true, "[\"T1078\"]", "System", 100, "[\"Verify system startup\",\"Review startup sequence\"]", "low", "Event log service was started", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 15, "Security", 60, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 6006, "SystemShutdown", true, "[\"T1078\"]", "System", 100, "[\"Verify system shutdown\",\"Review shutdown sequence\"]", "low", "Event log service was stopped", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 16, "Security", 85, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4719, "SecurityPolicyChange", true, "[\"T1562\"]", "System", 100, "[\"Investigate audit policy changes\",\"Verify administrative approval\",\"Review logging configuration\"]", "high", "System audit policy was changed", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 17, "Security", 85, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4902, "SecurityPolicyChange", true, "[\"T1562\"]", "System", 100, "[\"Review audit policy changes\",\"Verify policy modifications\"]", "high", "Per-user audit policy table was created", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 18, "Security", 85, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4904, "SecurityPolicyChange", true, "[\"T1562\"]", "System", 100, "[\"Investigate event source registration\",\"Verify administrative approval\"]", "high", "Security event source registration attempt", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 19, "Security", 85, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4905, "SecurityPolicyChange", true, "[\"T1562\"]", "System", 100, "[\"Investigate event source unregistration\",\"Verify administrative approval\"]", "high", "Security event source unregistration attempt", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20, "Security", 85, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4907, "SecurityPolicyChange", true, "[\"T1562\"]", "System", 100, "[\"Investigate audit setting changes\",\"Verify administrative approval\"]", "high", "Audit settings on object were changed", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 21, "Security", 85, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4908, "SecurityPolicyChange", true, "[\"T1562\"]", "System", 100, "[\"Investigate special groups changes\",\"Verify administrative approval\"]", "high", "Special Groups Logon table modified", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 22, "Security", 95, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 1102, "SecurityPolicyChange", true, "[\"T1070\",\"T1562\"]", "System", 100, "[\"Investigate log clearing\",\"Verify administrative approval\",\"Check for anti-forensics activity\"]", "critical", "Audit log was cleared", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 23, "Security", 70, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 5156, "NetworkConnection", true, "[\"T1071\"]", "System", 100, "[\"Review network connections\",\"Check for suspicious traffic patterns\"]", "medium", "Filtering Platform connection", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 24, "Security", 70, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 5157, "NetworkConnection", true, "[\"T1071\"]", "System", 100, "[\"Review blocked connections\",\"Check firewall rules\"]", "medium", "Filtering Platform connection blocked", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 25, "Microsoft-Windows-PowerShell/Operational", 80, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4104, "PowerShellExecution", true, "[\"T1059.001\"]", "System", 100, "[\"Review PowerShell script content\",\"Check for malicious commands\",\"Analyze script block patterns\"]", "medium", "PowerShell script block execution", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 26, "Microsoft-Windows-PowerShell/Operational", 60, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4103, "PowerShellExecution", true, "[\"T1059.001\"]", "System", 100, "[\"Review loaded PowerShell modules\",\"Check for suspicious module usage\"]", "low", "PowerShell module logging", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 27, "Microsoft-Windows-PowerShell/Operational", 70, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4105, "PowerShellExecution", true, "[\"T1059.001\"]", "System", 100, "[\"Review PowerShell pipeline activity\",\"Monitor for unusual execution patterns\"]", "medium", "PowerShell pipeline execution started", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 28, "Microsoft-Windows-PowerShell/Operational", 70, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4106, "PowerShellExecution", true, "[\"T1059.001\"]", "System", 100, "[\"Correlate with pipeline start events\",\"Review execution duration\"]", "medium", "PowerShell pipeline execution stopped", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 29, "Microsoft-Windows-PowerShell/Operational", 50, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4100, "PowerShellExecution", true, "[\"T1059.001\"]", "System", 100, "[\"Monitor provider activity\",\"Review provider security\"]", "low", "PowerShell provider started", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 30, "Microsoft-Windows-PowerShell/Operational", 50, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4101, "PowerShellExecution", true, "[\"T1059.001\"]", "System", 100, "[\"Correlate with provider start events\"]", "low", "PowerShell provider stopped", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 31, "Microsoft-Windows-PowerShell/Operational", 65, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 4102, "PowerShellExecution", true, "[\"T1059.001\"]", "System", 100, "[\"Investigate command health issues\",\"Review PowerShell security policies\"]", "medium", "PowerShell command health violation", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 32, "Microsoft-Windows-PowerShell/Operational", 55, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 400, "PowerShellExecution", true, "[\"T1059.001\"]", "System", 100, "[\"Monitor PowerShell engine lifecycle\",\"Review engine configuration\"]", "low", "PowerShell engine state changed", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 33, "Microsoft-Windows-PowerShell/Operational", 55, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 403, "PowerShellExecution", true, "[\"T1059.001\"]", "System", 100, "[\"Correlate with engine start events\",\"Review engine activity\"]", "low", "PowerShell engine stopped", null, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 1,
                column: "UpdatedAt",
                value: new DateTime(2025, 10, 1, 9, 29, 29, 861, DateTimeKind.Utc).AddTicks(4723));

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "UpdatedAt", "Value" },
                values: new object[] { new DateTime(2025, 10, 1, 9, 29, 29, 861, DateTimeKind.Utc).AddTicks(4822), "2025-10-01" });

            migrationBuilder.UpdateData(
                table: "SystemConfiguration",
                keyColumn: "Id",
                keyValue: 3,
                column: "UpdatedAt",
                value: new DateTime(2025, 10, 1, 9, 29, 29, 861, DateTimeKind.Utc).AddTicks(4824));

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEventRules_EnabledPriority",
                table: "SecurityEventRules",
                columns: new[] { "IsEnabled", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEventRules_EventChannel",
                table: "SecurityEventRules",
                columns: new[] { "EventId", "Channel" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEventRules_EventType",
                table: "SecurityEventRules",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEventRules_IsEnabled",
                table: "SecurityEventRules",
                column: "IsEnabled");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SecurityEventRules");

            migrationBuilder.CreateTable(
                name: "ComplianceControls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApplicableSectors = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ControlId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ControlName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Framework = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsUserVisible = table.Column<bool>(type: "INTEGER", nullable: false),
                    Priority = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false),
                    ValidationQuery = table.Column<string>(type: "text", nullable: true)
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
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FailedControls = table.Column<int>(type: "INTEGER", nullable: false),
                    Framework = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GapCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Generated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GeneratedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ImplementationPercentage = table.Column<int>(type: "INTEGER", nullable: false),
                    ImplementedControls = table.Column<int>(type: "INTEGER", nullable: false),
                    KeyFindings = table.Column<string>(type: "text", nullable: true),
                    NextReview = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Recommendations = table.Column<string>(type: "text", nullable: true),
                    ReportData = table.Column<string>(type: "text", nullable: true),
                    ReportType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RiskScore = table.Column<float>(type: "REAL", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    TotalControls = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceAssessmentResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReportId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    AssessedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ControlId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Evidence = table.Column<string>(type: "text", nullable: true),
                    Findings = table.Column<string>(type: "text", nullable: true),
                    Framework = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Recommendations = table.Column<string>(type: "text", nullable: true),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
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
        }
    }
}
