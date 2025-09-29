using System.Text;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.Extensions.Logging;

namespace Castellan.Worker.Services.Compliance;

public interface IOptimizedPdfReportService
{
    Task<byte[]> GenerateOptimizedPdfReportAsync(ComplianceReportDocument document);
}

public class OptimizedPdfReportService : IOptimizedPdfReportService
{
    private readonly ILogger<OptimizedPdfReportService> _logger;

    // Reusable font instances to avoid repeated creation
    private static readonly iTextSharp.text.Font TitleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.Black);
    private static readonly iTextSharp.text.Font SubtitleFont = FontFactory.GetFont(FontFactory.HELVETICA, 14, BaseColor.Black);
    private static readonly iTextSharp.text.Font HeaderFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, BaseColor.Black);
    private static readonly iTextSharp.text.Font NormalFont = FontFactory.GetFont(FontFactory.HELVETICA, 12, BaseColor.Black);
    private static readonly iTextSharp.text.Font SmallFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, BaseColor.Black);
    private static readonly iTextSharp.text.Font BoldSmallFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.Black);
    private static readonly iTextSharp.text.Font TinyFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.Black);

    public OptimizedPdfReportService(ILogger<OptimizedPdfReportService> logger)
    {
        _logger = logger;
    }

    public async Task<byte[]> GenerateOptimizedPdfReportAsync(ComplianceReportDocument document)
    {
        _logger.LogInformation("Generating optimized PDF report for {Title}", document.Title);

        using var stream = new MemoryStream();
        var pdfDocument = new Document(PageSize.A4, 40, 40, 40, 40); // Smaller margins
        var writer = PdfWriter.GetInstance(pdfDocument, stream);

        try
        {
            pdfDocument.Open();

            // Add content in optimized order
            await AddTitlePageAsync(pdfDocument, document);
            await AddExecutiveSummaryAsync(pdfDocument, document);
            await AddOverviewAsync(pdfDocument, document);
            await AddControlAssessmentSummaryAsync(pdfDocument, document);
            await AddRiskAnalysisAsync(pdfDocument, document);
            await AddRecommendationsAsync(pdfDocument, document);

            pdfDocument.Close();

            var result = stream.ToArray();
            _logger.LogInformation("Generated optimized PDF report: {Size} bytes", result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating optimized PDF report");
            pdfDocument?.Close();
            throw;
        }
    }

    private async Task AddTitlePageAsync(Document document, ComplianceReportDocument reportDoc)
    {
        // Title
        var title = new Paragraph(reportDoc.Title, TitleFont)
        {
            Alignment = Element.ALIGN_CENTER,
            SpacingAfter = 15
        };
        document.Add(title);

        // Framework
        var framework = new Paragraph($"Framework: {reportDoc.Framework}", SubtitleFont)
        {
            Alignment = Element.ALIGN_CENTER,
            SpacingAfter = 10
        };
        document.Add(framework);

        // Generated date
        var genDate = new Paragraph($"Generated: {reportDoc.GeneratedAt:yyyy-MM-dd HH:mm:ss UTC}", NormalFont)
        {
            Alignment = Element.ALIGN_CENTER,
            SpacingAfter = 20
        };
        document.Add(genDate);

        // Audience
        var audience = new Paragraph($"Audience: {reportDoc.Audience}", NormalFont)
        {
            Alignment = Element.ALIGN_CENTER,
            SpacingAfter = 30
        };
        document.Add(audience);

        await Task.CompletedTask;
    }

    private async Task AddExecutiveSummaryAsync(Document document, ComplianceReportDocument reportDoc)
    {
        if (reportDoc.ExecutiveSummary == null) return;

        var header = new Paragraph("Executive Summary", HeaderFont) { SpacingAfter = 10 };
        document.Add(header);

        // Summary metrics in a compact table
        var metricsTable = new PdfPTable(2) { WidthPercentage = 100 };
        metricsTable.SetWidths(new float[] { 1.5f, 1f });

        AddCompactTableRow(metricsTable, "Overall Score", $"{reportDoc.ExecutiveSummary.OverallScore:F1}%");
        AddCompactTableRow(metricsTable, "Risk Level", reportDoc.ExecutiveSummary.RiskLevel);

        document.Add(metricsTable);
        document.Add(new Paragraph(" ") { SpacingAfter = 10 });

        // Summary text
        var summaryPara = new Paragraph(reportDoc.ExecutiveSummary.Summary, NormalFont)
        {
            SpacingAfter = 15
        };
        document.Add(summaryPara);

        // Key findings as bullet points
        if (reportDoc.ExecutiveSummary.KeyFindings.Any())
        {
            var findingsHeader = new Paragraph("Key Findings:", BoldSmallFont) { SpacingAfter = 5 };
            document.Add(findingsHeader);

            foreach (var finding in reportDoc.ExecutiveSummary.KeyFindings.Take(5)) // Limit for performance
            {
                var bullet = new Paragraph($"• {finding}", SmallFont) { IndentationLeft = 20, SpacingAfter = 3 };
                document.Add(bullet);
            }
        }

        document.Add(new Paragraph(" ") { SpacingAfter = 15 });
        await Task.CompletedTask;
    }

    private async Task AddOverviewAsync(Document document, ComplianceReportDocument reportDoc)
    {
        if (reportDoc.Overview == null) return;

        var header = new Paragraph("Compliance Overview", HeaderFont) { SpacingAfter = 10 };
        document.Add(header);

        var overviewTable = new PdfPTable(2) { WidthPercentage = 100 };
        overviewTable.SetWidths(new float[] { 1.5f, 1f });

        AddCompactTableRow(overviewTable, "Total Controls", reportDoc.Overview.TotalControls.ToString());
        AddCompactTableRow(overviewTable, "Implemented", reportDoc.Overview.ImplementedControls.ToString());
        AddCompactTableRow(overviewTable, "Partially Implemented", reportDoc.Overview.PartiallyImplementedControls.ToString());
        AddCompactTableRow(overviewTable, "Not Implemented", reportDoc.Overview.NotImplementedControls.ToString());
        AddCompactTableRow(overviewTable, "Compliance %", $"{reportDoc.Overview.CompliancePercentage:F1}%");
        AddCompactTableRow(overviewTable, "Last Assessment", reportDoc.Overview.LastAssessment.ToString("yyyy-MM-dd"));

        document.Add(overviewTable);
        document.Add(new Paragraph(" ") { SpacingAfter = 15 });

        await Task.CompletedTask;
    }

    private async Task AddControlAssessmentSummaryAsync(Document document, ComplianceReportDocument reportDoc)
    {
        if (reportDoc.ControlAssessment?.Controls == null || !reportDoc.ControlAssessment.Controls.Any())
            return;

        var header = new Paragraph("Control Assessment Summary", HeaderFont) { SpacingAfter = 10 };
        document.Add(header);

        // Create a more compact table with essential info only
        var controlsTable = new PdfPTable(4) { WidthPercentage = 100 };
        controlsTable.SetWidths(new float[] { 2f, 1f, 1f, 0.8f });

        // Headers
        AddCompactTableHeader(controlsTable, "Control");
        AddCompactTableHeader(controlsTable, "Category");
        AddCompactTableHeader(controlsTable, "Status");
        AddCompactTableHeader(controlsTable, "Score");

        // Add first 15 controls for PDF performance
        var controlsToShow = reportDoc.ControlAssessment.Controls
            .OrderByDescending(c => c.Priority == "Critical" ? 3 : c.Priority == "High" ? 2 : 1)
            .Take(15);

        foreach (var control in controlsToShow)
        {
            AddCompactTableCell(controlsTable, TruncateText(control.ControlName, 40));
            AddCompactTableCell(controlsTable, control.Category);
            AddCompactTableCell(controlsTable, control.Status);
            AddCompactTableCell(controlsTable, $"{control.Score:F0}");
        }

        document.Add(controlsTable);

        if (reportDoc.ControlAssessment.Controls.Count > 15)
        {
            var note = new Paragraph($"Note: Showing top 15 of {reportDoc.ControlAssessment.Controls.Count} controls. Full details available in digital format.", TinyFont)
            {
                SpacingAfter = 15,
                Alignment = Element.ALIGN_CENTER
            };
            document.Add(note);
        }

        await Task.CompletedTask;
    }

    private async Task AddRiskAnalysisAsync(Document document, ComplianceReportDocument reportDoc)
    {
        if (reportDoc.RiskAnalysis == null) return;

        var header = new Paragraph("Risk Analysis", HeaderFont) { SpacingAfter = 10 };
        document.Add(header);

        var riskTable = new PdfPTable(2) { WidthPercentage = 100 };
        riskTable.SetWidths(new float[] { 1.5f, 1f });

        AddCompactTableRow(riskTable, "Overall Risk Score", $"{reportDoc.RiskAnalysis.OverallRiskScore:F1}");
        AddCompactTableRow(riskTable, "Risk Level", reportDoc.RiskAnalysis.RiskLevel);
        AddCompactTableRow(riskTable, "High Risk Areas", reportDoc.RiskAnalysis.HighRiskAreas.Count.ToString());
        AddCompactTableRow(riskTable, "Medium Risk Areas", reportDoc.RiskAnalysis.MediumRiskAreas.Count.ToString());

        document.Add(riskTable);

        // High risk areas
        if (reportDoc.RiskAnalysis.HighRiskAreas.Any())
        {
            document.Add(new Paragraph(" ") { SpacingAfter = 10 });
            var highRiskHeader = new Paragraph("Top High Risk Areas:", BoldSmallFont) { SpacingAfter = 5 };
            document.Add(highRiskHeader);

            foreach (var risk in reportDoc.RiskAnalysis.HighRiskAreas.Take(5))
            {
                var riskItem = new Paragraph($"• {TruncateText(risk.Area, 50)} (Score: {risk.RiskScore:F1})", SmallFont)
                {
                    IndentationLeft = 20,
                    SpacingAfter = 3
                };
                document.Add(riskItem);
            }
        }

        document.Add(new Paragraph(" ") { SpacingAfter = 15 });
        await Task.CompletedTask;
    }

    private async Task AddRecommendationsAsync(Document document, ComplianceReportDocument reportDoc)
    {
        if (reportDoc.Recommendations == null) return;

        var header = new Paragraph("Recommendations", HeaderFont) { SpacingAfter = 10 };
        document.Add(header);

        if (reportDoc.Recommendations.ImmediateActions.Any())
        {
            var immediateHeader = new Paragraph("Immediate Actions:", BoldSmallFont) { SpacingAfter = 5 };
            document.Add(immediateHeader);

            foreach (var action in reportDoc.Recommendations.ImmediateActions.Take(5))
            {
                var actionItem = new Paragraph($"• {TruncateText(action.Title, 60)}", SmallFont)
                {
                    IndentationLeft = 20,
                    SpacingAfter = 3
                };
                document.Add(actionItem);
            }
            document.Add(new Paragraph(" ") { SpacingAfter = 10 });
        }

        if (reportDoc.Recommendations.ShortTermActions.Any())
        {
            var shortTermHeader = new Paragraph("Short-term Actions:", BoldSmallFont) { SpacingAfter = 5 };
            document.Add(shortTermHeader);

            foreach (var action in reportDoc.Recommendations.ShortTermActions.Take(3))
            {
                var actionItem = new Paragraph($"• {TruncateText(action.Title, 60)}", SmallFont)
                {
                    IndentationLeft = 20,
                    SpacingAfter = 3
                };
                document.Add(actionItem);
            }
        }

        await Task.CompletedTask;
    }

    private void AddCompactTableRow(PdfPTable table, string label, string value)
    {
        table.AddCell(new PdfPCell(new Phrase(label, BoldSmallFont))
        {
            BackgroundColor = BaseColor.LightGray,
            Padding = 4,
            Border = iTextSharp.text.Rectangle.BOX
        });
        table.AddCell(new PdfPCell(new Phrase(value, SmallFont))
        {
            Padding = 4,
            Border = iTextSharp.text.Rectangle.BOX
        });
    }

    private void AddCompactTableHeader(PdfPTable table, string text)
    {
        table.AddCell(new PdfPCell(new Phrase(text, BoldSmallFont))
        {
            BackgroundColor = BaseColor.DarkGray,
            Padding = 4,
            HorizontalAlignment = Element.ALIGN_CENTER,
            Border = iTextSharp.text.Rectangle.BOX
        });
    }

    private void AddCompactTableCell(PdfPTable table, string text)
    {
        table.AddCell(new PdfPCell(new Phrase(text, TinyFont))
        {
            Padding = 3,
            Border = iTextSharp.text.Rectangle.BOX
        });
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength - 3) + "...";
    }
}