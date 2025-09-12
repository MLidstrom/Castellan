using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Castellan.Worker.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvancedSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add composite indexes for advanced search performance
            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_TimestampRiskEvent",
                table: "SecurityEvents",
                columns: new[] { "Timestamp", "RiskLevel", "EventType" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_RiskTimestamp",
                table: "SecurityEvents",
                columns: new[] { "RiskLevel", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_EventTypeTimestamp",
                table: "SecurityEvents",
                columns: new[] { "EventType", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_ConfidenceCorrelation",
                table: "SecurityEvents",
                columns: new[] { "Confidence", "CorrelationScore" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_CreatedAt",
                table: "SecurityEvents",
                column: "CreatedAt");

            // Create FTS5 virtual table for full-text search
            migrationBuilder.Sql(@"
                CREATE VIRTUAL TABLE SecurityEvents_FTS USING fts5(
                    EventId UNINDEXED,
                    Message,
                    Summary, 
                    EventData,
                    content='SecurityEvents',
                    content_rowid='Id'
                );
            ");

            // Create triggers to keep FTS table synchronized
            migrationBuilder.Sql(@"
                CREATE TRIGGER SecurityEvents_FTS_Insert AFTER INSERT ON SecurityEvents BEGIN
                    INSERT INTO SecurityEvents_FTS(rowid, EventId, Message, Summary, EventData) 
                    VALUES (new.Id, new.EventId, new.Message, new.Summary, new.EventData);
                END;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER SecurityEvents_FTS_Delete AFTER DELETE ON SecurityEvents BEGIN
                    INSERT INTO SecurityEvents_FTS(SecurityEvents_FTS, rowid, EventId, Message, Summary, EventData) 
                    VALUES('delete', old.Id, old.EventId, old.Message, old.Summary, old.EventData);
                END;
            ");

            migrationBuilder.Sql(@"
                CREATE TRIGGER SecurityEvents_FTS_Update AFTER UPDATE ON SecurityEvents BEGIN
                    INSERT INTO SecurityEvents_FTS(SecurityEvents_FTS, rowid, EventId, Message, Summary, EventData) 
                    VALUES('delete', old.Id, old.EventId, old.Message, old.Summary, old.EventData);
                    INSERT INTO SecurityEvents_FTS(rowid, EventId, Message, Summary, EventData) 
                    VALUES (new.Id, new.EventId, new.Message, new.Summary, new.EventData);
                END;
            ");

            // Populate FTS table with existing data
            migrationBuilder.Sql(@"
                INSERT INTO SecurityEvents_FTS(rowid, EventId, Message, Summary, EventData)
                SELECT Id, EventId, Message, Summary, EventData FROM SecurityEvents;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop FTS triggers and table
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS SecurityEvents_FTS_Insert;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS SecurityEvents_FTS_Delete;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS SecurityEvents_FTS_Update;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS SecurityEvents_FTS;");

            // Drop performance indexes
            migrationBuilder.DropIndex(
                name: "IX_SecurityEvents_CreatedAt",
                table: "SecurityEvents");

            migrationBuilder.DropIndex(
                name: "IX_SecurityEvents_ConfidenceCorrelation",
                table: "SecurityEvents");

            migrationBuilder.DropIndex(
                name: "IX_SecurityEvents_EventTypeTimestamp",
                table: "SecurityEvents");

            migrationBuilder.DropIndex(
                name: "IX_SecurityEvents_RiskTimestamp",
                table: "SecurityEvents");

            migrationBuilder.DropIndex(
                name: "IX_SecurityEvents_TimestampRiskEvent",
                table: "SecurityEvents");
        }
    }
}
