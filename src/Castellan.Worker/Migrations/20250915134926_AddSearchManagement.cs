using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Castellan.Worker.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Applications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    SecurityProfile = table.Column<string>(type: "TEXT", nullable: true),
                    RiskScore = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CloudSecurityEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventId = table.Column<string>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    EventType = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    UserPrincipalName = table.Column<string>(type: "TEXT", nullable: false),
                    ClientAppUsed = table.Column<string>(type: "TEXT", nullable: false),
                    IpAddress = table.Column<string>(type: "TEXT", nullable: false),
                    Location = table.Column<string>(type: "TEXT", nullable: false),
                    RiskLevel = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    RawData = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CloudSecurityEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MitreTechniques",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TechniqueId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Tactic = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DataSources = table.Column<string>(type: "TEXT", nullable: true),
                    Mitigations = table.Column<string>(type: "TEXT", nullable: true),
                    Examples = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MitreTechniques", x => x.Id);
                    table.UniqueConstraint("AK_MitreTechniques_TechniqueId", x => x.TechniqueId);
                });

            migrationBuilder.CreateTable(
                name: "SavedSearches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    SearchFilters = table.Column<string>(type: "TEXT", nullable: false),
                    IsPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UseCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedSearches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SearchHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    SearchFilters = table.Column<string>(type: "TEXT", nullable: false),
                    SearchHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ResultCount = table.Column<int>(type: "INTEGER", nullable: true),
                    ExecutionTimeMs = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemConfiguration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemConfiguration", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MalwareMatches",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RuleId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RuleName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    MatchTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TargetFile = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    TargetHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    MatchedStrings = table.Column<string>(type: "TEXT", nullable: false),
                    Metadata = table.Column<string>(type: "TEXT", nullable: false),
                    ExecutionTimeMs = table.Column<double>(type: "REAL", nullable: false),
                    SecurityEventId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MalwareMatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MalwareRules",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    RuleContent = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Author = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    ThreatLevel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    HitCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FalsePositiveCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AverageExecutionTimeMs = table.Column<double>(type: "REAL", nullable: false),
                    MitreTechniques = table.Column<string>(type: "TEXT", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousVersion = table.Column<string>(type: "TEXT", nullable: true),
                    IsValid = table.Column<bool>(type: "INTEGER", nullable: false),
                    ValidationError = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    LastValidated = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TestSample = table.Column<string>(type: "TEXT", nullable: true),
                    TestResult = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MalwareRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecurityEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    RiskLevel = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Message = table.Column<string>(type: "TEXT", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    EventData = table.Column<string>(type: "TEXT", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SourceIp = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    DestinationIp = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    ApplicationId = table.Column<int>(type: "INTEGER", nullable: true),
                    MitreTechniques = table.Column<string>(type: "TEXT", nullable: true),
                    RecommendedActions = table.Column<string>(type: "TEXT", nullable: true),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    CorrelationScore = table.Column<double>(type: "REAL", nullable: false),
                    BurstScore = table.Column<double>(type: "REAL", nullable: false),
                    AnomalyScore = table.Column<double>(type: "REAL", nullable: false),
                    IsDeterministic = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsCorrelationBased = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsEnhanced = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnrichmentData = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SecurityEvents_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationMitreAssociations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    TechniqueId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationMitreAssociations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationMitreAssociations_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApplicationMitreAssociations_MitreTechniques_TechniqueId",
                        column: x => x.TechniqueId,
                        principalTable: "MitreTechniques",
                        principalColumn: "TechniqueId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "MitreTechniques",
                columns: new[] { "Id", "CreatedAt", "DataSources", "Description", "Examples", "Mitigations", "Name", "Platform", "Tactic", "TechniqueId" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 9, 15, 13, 49, 25, 518, DateTimeKind.Utc).AddTicks(8909), null, "Adversaries may inject code into processes in order to evade process-based defenses as well as possibly elevate privileges.", null, null, "Process Injection", "Windows, macOS, Linux", "Defense Evasion", "T1055" },
                    { 2, new DateTime(2025, 9, 15, 13, 49, 25, 518, DateTimeKind.Utc).AddTicks(8912), null, "Adversaries may abuse command and script interpreters to execute commands, scripts, or binaries.", null, null, "Command and Scripting Interpreter", "Windows, macOS, Linux", "Execution", "T1059" },
                    { 3, new DateTime(2025, 9, 15, 13, 49, 25, 518, DateTimeKind.Utc).AddTicks(8914), null, "Adversaries may enumerate files and directories or may search in specific locations of a host or network share for certain information within a file system.", null, null, "File and Directory Discovery", "Windows, macOS, Linux", "Discovery", "T1083" }
                });

            migrationBuilder.InsertData(
                table: "SystemConfiguration",
                columns: new[] { "Id", "Description", "Key", "UpdatedAt", "Value" },
                values: new object[,]
                {
                    { 1, "Current database schema version", "DatabaseVersion", new DateTime(2025, 9, 15, 13, 49, 25, 518, DateTimeKind.Utc).AddTicks(9091), "1.0.0" },
                    { 2, "Last date MITRE ATT&CK data was updated", "LastMitreUpdate", new DateTime(2025, 9, 15, 13, 49, 25, 518, DateTimeKind.Utc).AddTicks(9201), "2025-09-15" },
                    { 3, "Last date YARA rules were fetched and updated", "LastMalwareRulesUpdate", new DateTime(2025, 9, 15, 13, 49, 25, 518, DateTimeKind.Utc).AddTicks(9202), "1970-01-01" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationMitreAssociations_ApplicationId_TechniqueId",
                table: "ApplicationMitreAssociations",
                columns: new[] { "ApplicationId", "TechniqueId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationMitreAssociations_TechniqueId",
                table: "ApplicationMitreAssociations",
                column: "TechniqueId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Name",
                table: "Applications",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_MitreTechniques_TechniqueId",
                table: "MitreTechniques",
                column: "TechniqueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedSearches_CreatedAt",
                table: "SavedSearches",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SavedSearches_LastUsedAt",
                table: "SavedSearches",
                column: "LastUsedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SavedSearches_Name",
                table: "SavedSearches",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_SavedSearches_UserId",
                table: "SavedSearches",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedSearches_UserName",
                table: "SavedSearches",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SearchHistory_CreatedAt",
                table: "SearchHistory",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SearchHistory_SearchHash",
                table: "SearchHistory",
                column: "SearchHash");

            migrationBuilder.CreateIndex(
                name: "IX_SearchHistory_UserHash",
                table: "SearchHistory",
                columns: new[] { "UserId", "SearchHash" });

            migrationBuilder.CreateIndex(
                name: "IX_SearchHistory_UserId",
                table: "SearchHistory",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SearchHistory_UserTime",
                table: "SearchHistory",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_ApplicationId",
                table: "SecurityEvents",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_ConfidenceCorrelation",
                table: "SecurityEvents",
                columns: new[] { "Confidence", "CorrelationScore" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_CreatedAt",
                table: "SecurityEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_EventId",
                table: "SecurityEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_EventType",
                table: "SecurityEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_EventTypeTimestamp",
                table: "SecurityEvents",
                columns: new[] { "EventType", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_RiskLevel",
                table: "SecurityEvents",
                column: "RiskLevel");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_RiskTimestamp",
                table: "SecurityEvents",
                columns: new[] { "RiskLevel", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_Severity",
                table: "SecurityEvents",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_Timestamp",
                table: "SecurityEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_TimestampRiskEvent",
                table: "SecurityEvents",
                columns: new[] { "Timestamp", "RiskLevel", "EventType" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemConfiguration_Key",
                table: "SystemConfiguration",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MalwareMatches_MatchTime",
                table: "MalwareMatches",
                column: "MatchTime");

            migrationBuilder.CreateIndex(
                name: "IX_MalwareMatches_RuleId",
                table: "MalwareMatches",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_MalwareMatches_RuleTime",
                table: "MalwareMatches",
                columns: new[] { "RuleId", "MatchTime" });

            migrationBuilder.CreateIndex(
                name: "IX_MalwareMatches_TargetHash",
                table: "MalwareMatches",
                column: "TargetHash");

            migrationBuilder.CreateIndex(
                name: "IX_MalwareRules_Category",
                table: "MalwareRules",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_MalwareRules_CreatedAt",
                table: "MalwareRules",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MalwareRules_EnabledCategory",
                table: "MalwareRules",
                columns: new[] { "IsEnabled", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_MalwareRules_IsEnabled",
                table: "MalwareRules",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_MalwareRules_Name",
                table: "MalwareRules",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MalwareRules_UpdatedAt",
                table: "MalwareRules",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationMitreAssociations");

            migrationBuilder.DropTable(
                name: "CloudSecurityEvents");

            migrationBuilder.DropTable(
                name: "SavedSearches");

            migrationBuilder.DropTable(
                name: "SearchHistory");

            migrationBuilder.DropTable(
                name: "SecurityEvents");

            migrationBuilder.DropTable(
                name: "SystemConfiguration");

            migrationBuilder.DropTable(
                name: "MalwareMatches");

            migrationBuilder.DropTable(
                name: "MalwareRules");

            migrationBuilder.DropTable(
                name: "MitreTechniques");

            migrationBuilder.DropTable(
                name: "Applications");
        }
    }
}
