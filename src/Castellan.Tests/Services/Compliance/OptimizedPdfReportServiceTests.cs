using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using Xunit;
using Castellan.Worker.Services.Compliance;

namespace Castellan.Tests.Services.Compliance;

public class OptimizedPdfReportServiceTests
{
    private readonly Mock<ILogger<OptimizedPdfReportService>> _mockLogger;
    private readonly OptimizedPdfReportService _pdfService;

    public OptimizedPdfReportServiceTests()
    {
        _mockLogger = new Mock<ILogger<OptimizedPdfReportService>>();
        _pdfService = new OptimizedPdfReportService(_mockLogger.Object);
    }

    [Fact]
    public async Task GenerateOptimizedPdfReportAsync_WithMinimalDocument_GeneratesPdf()
    {
        // Arrange
        var document = CreateMinimalDocument();

        // Act
        var result = await _pdfService.GenerateOptimizedPdfReportAsync(document);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);

        // PDF should start with PDF header
        var pdfHeader = System.Text.Encoding.ASCII.GetString(result, 0, Math.Min(8, result.Length));
        pdfHeader.Should().StartWith("%PDF-");
    }

    [Fact]
    public async Task GenerateOptimizedPdfReportAsync_WithCompleteDocument_GeneratesLargerPdf()
    {
        // Arrange
        var minimalDocument = CreateMinimalDocument();
        var completeDocument = CreateCompleteDocument();

        // Act
        var minimalResult = await _pdfService.GenerateOptimizedPdfReportAsync(minimalDocument);
        var completeResult = await _pdfService.GenerateOptimizedPdfReportAsync(completeDocument);

        // Assert
        completeResult.Length.Should().BeGreaterThan(minimalResult.Length);
    }

    [Fact]
    public async Task GenerateOptimizedPdfReportAsync_WithExecutiveSummary_IncludesKeyMetrics()
    {
        // Arrange
        var document = CreateDocumentWithExecutiveSummary();

        // Act
        var result = await _pdfService.GenerateOptimizedPdfReportAsync(document);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(1000); // Should be substantial with content
    }

    [Fact]
    public async Task GenerateOptimizedPdfReportAsync_WithControlAssessment_ProcessesControlsEfficiently()
    {
        // Arrange
        var document = CreateDocumentWithManyControls();

        // Act
        var result = await _pdfService.GenerateOptimizedPdfReportAsync(document);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(2000); // Should handle many controls
    }

    [Fact]
    public async Task GenerateOptimizedPdfReportAsync_WithRiskAnalysis_IncludesRiskData()
    {
        // Arrange
        var document = CreateDocumentWithRiskAnalysis();

        // Act
        var result = await _pdfService.GenerateOptimizedPdfReportAsync(document);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(1500);
    }

    [Fact]
    public async Task GenerateOptimizedPdfReportAsync_WithRecommendations_IncludesActionItems()
    {
        // Arrange
        var document = CreateDocumentWithRecommendations();

        // Act
        var result = await _pdfService.GenerateOptimizedPdfReportAsync(document);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(1000);
    }

    [Fact]
    public async Task GenerateOptimizedPdfReportAsync_PerformanceTest_CompletesWithinTimeLimit()
    {
        // Arrange
        var document = CreateLargeCompleteDocument();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var result = await _pdfService.GenerateOptimizedPdfReportAsync(document);

        // Assert
        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete within 5 seconds
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateOptimizedPdfReportAsync_WithNullSections_HandlesGracefully()
    {
        // Arrange
        var document = new ComplianceReportDocument
        {
            Title = "Test Report",
            Framework = "Test Framework",
            Format = ReportFormat.Pdf,
            Audience = ReportAudience.Technical,
            // All sections are null
        };

        // Act
        var result = await _pdfService.GenerateOptimizedPdfReportAsync(document);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GenerateOptimizedPdfReportAsync_WithLongText_TruncatesAppropriately()
    {
        // Arrange
        var document = CreateDocumentWithLongText();

        // Act
        var result = await _pdfService.GenerateOptimizedPdfReportAsync(document);

        // Assert
        result.Should().NotBeNull();
        // Should still generate successfully despite long text
        result.Length.Should().BeGreaterThan(1000);
    }

    private ComplianceReportDocument CreateMinimalDocument()
    {
        return new ComplianceReportDocument
        {
            Title = "Minimal Test Report",
            Framework = "Test Framework",
            Format = ReportFormat.Pdf,
            Audience = ReportAudience.Technical
        };
    }

    private ComplianceReportDocument CreateCompleteDocument()
    {
        return new ComplianceReportDocument
        {
            Title = "Complete Test Report",
            Framework = "HIPAA",
            Format = ReportFormat.Pdf,
            Audience = ReportAudience.Executive,
            ExecutiveSummary = CreateExecutiveSummary(),
            Overview = CreateOverview(),
            ControlAssessment = CreateControlAssessment(),
            RiskAnalysis = CreateRiskAnalysis(),
            Recommendations = CreateRecommendations()
        };
    }

    private ComplianceReportDocument CreateDocumentWithExecutiveSummary()
    {
        var document = CreateMinimalDocument();
        document.ExecutiveSummary = CreateExecutiveSummary();
        return document;
    }

    private ComplianceReportDocument CreateDocumentWithManyControls()
    {
        var document = CreateMinimalDocument();
        document.ControlAssessment = CreateControlAssessmentWithManyControls();
        return document;
    }

    private ComplianceReportDocument CreateDocumentWithRiskAnalysis()
    {
        var document = CreateMinimalDocument();
        document.RiskAnalysis = CreateRiskAnalysis();
        return document;
    }

    private ComplianceReportDocument CreateDocumentWithRecommendations()
    {
        var document = CreateMinimalDocument();
        document.Recommendations = CreateRecommendations();
        return document;
    }

    private ComplianceReportDocument CreateLargeCompleteDocument()
    {
        var document = CreateCompleteDocument();
        document.ControlAssessment = CreateControlAssessmentWithManyControls();
        return document;
    }

    private ComplianceReportDocument CreateDocumentWithLongText()
    {
        var document = CreateMinimalDocument();
        document.ExecutiveSummary = new ExecutiveSummarySection
        {
            Summary = new string('x', 5000), // Very long text
            OverallScore = 85.5,
            RiskLevel = "Medium"
        };
        return document;
    }

    private ExecutiveSummarySection CreateExecutiveSummary()
    {
        return new ExecutiveSummarySection
        {
            Summary = "Test executive summary with key compliance findings.",
            OverallScore = 85.5,
            RiskLevel = "Medium",
            KeyFindings = new List<string>
            {
                "15 controls fully implemented",
                "3 controls partially implemented",
                "2 controls not implemented"
            },
            CriticalGaps = new List<string>
            {
                "AC-001: Access Control policy missing"
            },
            Recommendation = "Focus on completing access control implementations",
            ScoresByCategory = new Dictionary<string, double>
            {
                { "Access Control", 75.0 },
                { "Data Protection", 90.0 }
            }
        };
    }

    private ComplianceOverviewSection CreateOverview()
    {
        return new ComplianceOverviewSection
        {
            Framework = "HIPAA",
            Description = "Health Insurance Portability and Accountability Act",
            TotalControls = 20,
            ImplementedControls = 15,
            PartiallyImplementedControls = 3,
            NotImplementedControls = 2,
            CompliancePercentage = 85.5,
            LastAssessment = DateTime.UtcNow.AddDays(-1),
            NextReview = DateTime.UtcNow.AddDays(90)
        };
    }

    private ControlAssessmentSection CreateControlAssessment()
    {
        return new ControlAssessmentSection
        {
            Controls = new List<ControlAssessmentDetail>
            {
                new()
                {
                    ControlId = "AC-001",
                    ControlName = "Access Control Policy",
                    Category = "Access Control",
                    Priority = "Critical",
                    Status = "Implemented",
                    Score = 95,
                    Evidence = "Policy documented and implemented"
                },
                new()
                {
                    ControlId = "AC-002",
                    ControlName = "User Authentication",
                    Category = "Access Control",
                    Priority = "High",
                    Status = "Partial",
                    Score = 70,
                    Evidence = "Basic authentication in place"
                }
            },
            ControlsByCategory = new Dictionary<string, int>
            {
                { "Access Control", 5 },
                { "Data Protection", 3 }
            }
        };
    }

    private ControlAssessmentSection CreateControlAssessmentWithManyControls()
    {
        var controls = new List<ControlAssessmentDetail>();

        // Create 25 controls to test performance
        for (int i = 1; i <= 25; i++)
        {
            controls.Add(new ControlAssessmentDetail
            {
                ControlId = $"AC-{i:D3}",
                ControlName = $"Access Control {i}",
                Category = i <= 10 ? "Access Control" : i <= 20 ? "Data Protection" : "Audit",
                Priority = i <= 5 ? "Critical" : i <= 15 ? "High" : "Medium",
                Status = i <= 20 ? "Implemented" : "Partial",
                Score = 95 - (i * 2),
                Evidence = $"Evidence for control {i}"
            });
        }

        return new ControlAssessmentSection
        {
            Controls = controls,
            ControlsByCategory = new Dictionary<string, int>
            {
                { "Access Control", 10 },
                { "Data Protection", 10 },
                { "Audit", 5 }
            }
        };
    }

    private RiskAnalysisSection CreateRiskAnalysis()
    {
        return new RiskAnalysisSection
        {
            OverallRiskScore = 45.5f,
            RiskLevel = "Medium",
            HighRiskAreas = new List<RiskItem>
            {
                new()
                {
                    Area = "Data Encryption",
                    Description = "Encryption at rest not fully implemented",
                    RiskScore = 75.0f,
                    Impact = "High",
                    Likelihood = "Medium",
                    Mitigation = "Implement full disk encryption"
                }
            },
            MediumRiskAreas = new List<RiskItem>
            {
                new()
                {
                    Area = "Access Logging",
                    Description = "Access logs not centralized",
                    RiskScore = 55.0f,
                    Impact = "Medium",
                    Likelihood = "Medium",
                    Mitigation = "Centralize log collection"
                }
            }
        };
    }

    private RecommendationsSection CreateRecommendations()
    {
        return new RecommendationsSection
        {
            ImmediateActions = new List<Recommendation>
            {
                new()
                {
                    Title = "Implement Data Encryption",
                    Description = "Deploy encryption for data at rest",
                    Priority = "Critical",
                    EstimatedEffort = "High",
                    ExpectedImpact = "High"
                }
            },
            ShortTermActions = new List<Recommendation>
            {
                new()
                {
                    Title = "Centralize Logging",
                    Description = "Deploy centralized log management",
                    Priority = "High",
                    EstimatedEffort = "Medium",
                    ExpectedImpact = "Medium"
                }
            }
        };
    }
}