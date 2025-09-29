using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using Xunit;
using Castellan.Worker.Data;
using Castellan.Worker.Models.Compliance;
using Castellan.Worker.Services.Compliance;
using Castellan.Worker.Services.Interfaces;

namespace Castellan.Tests.Services.Compliance;

public class ComplianceReportGenerationServiceTests : IDisposable
{
    private readonly CastellanDbContext _context;
    private readonly Mock<ILogger<ComplianceReportGenerationService>> _mockLogger;
    private readonly Mock<IComplianceAssessmentService> _mockAssessmentService;
    private readonly Mock<IComplianceFrameworkService> _mockFrameworkService;
    private readonly Mock<IComplianceReportCacheService> _mockCacheService;
    private readonly Mock<IOptimizedPdfReportService> _mockPdfService;
    private readonly ComplianceReportGenerationService _reportService;

    public ComplianceReportGenerationServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<CastellanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new CastellanDbContext(options);

        // Setup mocks
        _mockLogger = new Mock<ILogger<ComplianceReportGenerationService>>();
        _mockAssessmentService = new Mock<IComplianceAssessmentService>();
        _mockFrameworkService = new Mock<IComplianceFrameworkService>();
        _mockCacheService = new Mock<IComplianceReportCacheService>();
        _mockPdfService = new Mock<IOptimizedPdfReportService>();

        _reportService = new ComplianceReportGenerationService(
            _mockLogger.Object,
            _context,
            _mockAssessmentService.Object,
            _mockFrameworkService.Object,
            _mockCacheService.Object,
            _mockPdfService.Object
        );

        // Setup test data
        SeedTestData();
    }

    [Fact]
    public async Task GenerateComprehensiveReportAsync_WithValidFramework_GeneratesReport()
    {
        // Arrange
        var framework = "HIPAA";
        _mockCacheService.Setup(x => x.GetCachedReportAsync<ComplianceReportDocument>(It.IsAny<string>()))
            .ReturnsAsync((ComplianceReportDocument?)null);

        // Act
        var result = await _reportService.GenerateComprehensiveReportAsync(framework);

        // Assert
        result.Should().NotBeNull();
        result.Framework.Should().Be(framework);
        result.Title.Should().Contain(framework);
        result.IsOrganizationScope.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateComprehensiveReportAsync_WithCachedReport_ReturnsCachedVersion()
    {
        // Arrange
        var framework = "HIPAA";
        var cachedReport = new ComplianceReportDocument
        {
            Title = "Cached Report",
            Framework = framework
        };

        _mockCacheService.Setup(x => x.GetCachedReportAsync<ComplianceReportDocument>(It.IsAny<string>()))
            .ReturnsAsync(cachedReport);

        // Act
        var result = await _reportService.GenerateComprehensiveReportAsync(framework);

        // Assert
        result.Should().BeSameAs(cachedReport);
        _mockCacheService.Verify(x => x.SetCachedReportAsync(It.IsAny<string>(), It.IsAny<ComplianceReportDocument>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    [Fact]
    public async Task GenerateComprehensiveReportAsync_WithInvalidFramework_ThrowsException()
    {
        // Arrange
        var invalidFramework = "INVALID_FRAMEWORK";

        // Act & Assert
        var act = async () => await _reportService.GenerateComprehensiveReportAsync(invalidFramework);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not accessible for reporting*");
    }

    [Fact]
    public async Task GenerateComprehensiveReportAsync_CachesGeneratedReport()
    {
        // Arrange
        var framework = "HIPAA";
        _mockCacheService.Setup(x => x.GetCachedReportAsync<ComplianceReportDocument>(It.IsAny<string>()))
            .ReturnsAsync((ComplianceReportDocument?)null);

        // Act
        await _reportService.GenerateComprehensiveReportAsync(framework);

        // Assert
        _mockCacheService.Verify(x => x.SetCachedReportAsync(
            It.IsAny<string>(),
            It.IsAny<ComplianceReportDocument>(),
            TimeSpan.FromMinutes(10)), Times.Once);
    }

    [Fact]
    public async Task GenerateExecutiveSummaryAsync_WithMultipleFrameworks_GeneratesMultiFrameworkReport()
    {
        // Arrange
        var frameworks = new List<string> { "HIPAA", "SOX" };
        _mockCacheService.Setup(x => x.GetCachedReportAsync<ComplianceReportDocument>(It.IsAny<string>()))
            .ReturnsAsync((ComplianceReportDocument?)null);

        // Act
        var result = await _reportService.GenerateExecutiveSummaryAsync(frameworks);

        // Assert
        result.Should().NotBeNull();
        result.Framework.Should().Be("Multi-Framework");
        result.Audience.Should().Be(ReportAudience.Executive);
        result.ExecutiveSummary.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateExecutiveSummaryAsync_WithNullFrameworks_UsesAllOrganizationFrameworks()
    {
        // Arrange
        _mockCacheService.Setup(x => x.GetCachedReportAsync<ComplianceReportDocument>(It.IsAny<string>()))
            .ReturnsAsync((ComplianceReportDocument?)null);

        // Act
        var result = await _reportService.GenerateExecutiveSummaryAsync(null);

        // Assert
        result.Should().NotBeNull();
        result.Framework.Should().Be("Multi-Framework");
    }

    [Fact]
    public async Task GenerateComparisonReportAsync_WithValidFrameworks_GeneratesComparison()
    {
        // Arrange
        var frameworks = new List<string> { "HIPAA", "SOX" };

        // Act
        var result = await _reportService.GenerateComparisonReportAsync(frameworks);

        // Assert
        result.Should().NotBeNull();
        result.Framework.Should().Contain("vs");
        result.Audience.Should().Be(ReportAudience.Technical);
        result.Overview.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateComparisonReportAsync_WithInsufficientFrameworks_ThrowsException()
    {
        // Arrange
        var frameworks = new List<string> { "HIPAA" }; // Only one framework

        // Act & Assert
        var act = async () => await _reportService.GenerateComparisonReportAsync(frameworks);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*least 2 frameworks*");
    }

    [Fact]
    public async Task GenerateTrendReportAsync_WithValidFramework_GeneratesTrendReport()
    {
        // Arrange
        var framework = "HIPAA";
        var days = 30;

        // Add historical data
        await SeedHistoricalReports(framework);

        // Act
        var result = await _reportService.GenerateTrendReportAsync(framework, days);

        // Assert
        result.Should().NotBeNull();
        result.Framework.Should().Be(framework);
        result.Audience.Should().Be(ReportAudience.Operations);
        result.TrendAnalysis.Should().NotBeNull();
        result.TrendAnalysis!.TrendData.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateTrendReportAsync_WithNoHistoricalData_ThrowsException()
    {
        // Arrange
        var framework = "SOX";
        var days = 30;

        // Act & Assert
        var act = async () => await _reportService.GenerateTrendReportAsync(framework, days);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No historical data*");
    }

    [Fact]
    public async Task ExportReportAsync_WithJsonFormat_ReturnsJsonData()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act
        var result = await _reportService.ExportReportAsync(document, ReportFormat.Json);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);

        // Should be valid JSON
        var json = System.Text.Encoding.UTF8.GetString(result);
        json.Should().Contain("\"Title\"");
        json.Should().Contain("\"Framework\"");
    }

    [Fact]
    public async Task ExportReportAsync_WithHtmlFormat_ReturnsHtmlData()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act
        var result = await _reportService.ExportReportAsync(document, ReportFormat.Html);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);

        var html = System.Text.Encoding.UTF8.GetString(result);
        html.Should().Contain("<!DOCTYPE html>");
        html.Should().Contain("<title>");
    }

    [Fact]
    public async Task ExportReportAsync_WithPdfFormat_CallsOptimizedPdfService()
    {
        // Arrange
        var document = CreateTestDocument();
        var expectedPdfData = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF

        _mockPdfService.Setup(x => x.GenerateOptimizedPdfReportAsync(document))
            .ReturnsAsync(expectedPdfData);

        // Act
        var result = await _reportService.ExportReportAsync(document, ReportFormat.Pdf);

        // Assert
        result.Should().BeSameAs(expectedPdfData);
        _mockPdfService.Verify(x => x.GenerateOptimizedPdfReportAsync(document), Times.Once);
    }

    [Fact]
    public async Task ExportReportAsync_WithCsvFormat_ReturnsCsvData()
    {
        // Arrange
        var document = CreateTestDocumentWithControls();

        // Act
        var result = await _reportService.ExportReportAsync(document, ReportFormat.Csv);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);

        var csv = System.Text.Encoding.UTF8.GetString(result);
        csv.Should().Contain("Control ID");
        csv.Should().Contain("Control Name");
    }

    [Fact]
    public async Task ExportReportAsync_WithMarkdownFormat_ReturnsMarkdownData()
    {
        // Arrange
        var document = CreateTestDocument();

        // Act
        var result = await _reportService.ExportReportAsync(document, ReportFormat.Markdown);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);

        var markdown = System.Text.Encoding.UTF8.GetString(result);
        markdown.Should().Contain("# ");
        markdown.Should().Contain("**Generated:**");
    }

    [Fact]
    public async Task ExportReportAsync_WithUnsupportedFormat_ThrowsException()
    {
        // Arrange
        var document = CreateTestDocument();
        var unsupportedFormat = (ReportFormat)999;

        // Act & Assert
        var act = async () => await _reportService.ExportReportAsync(document, unsupportedFormat);
        await act.Should().ThrowAsync<NotSupportedException>();
    }

    private void SeedTestData()
    {
        // Add test compliance report
        var report = new ComplianceReport
        {
            Id = Guid.NewGuid().ToString(),
            Framework = "HIPAA",
            TotalControls = 17,
            ImplementedControls = 12,
            FailedControls = 2,
            ImplementationPercentage = 85,
            RiskScore = 45.5f,
            Generated = DateTime.UtcNow.AddDays(-1),
            NextReview = DateTime.UtcNow.AddDays(90),
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        _context.ComplianceReports.Add(report);

        // Add test controls
        var controls = new List<ComplianceControl>
        {
            new()
            {
                Id = 1,
                ControlId = "HIPAA-001",
                Framework = "HIPAA",
                ControlName = "Access Control",
                Category = "Access Control",
                Priority = "Critical",
                IsUserVisible = true,
                Scope = ComplianceScope.Organization
            },
            new()
            {
                Id = 2,
                ControlId = "SOX-001",
                Framework = "SOX",
                ControlName = "Financial Controls",
                Category = "Financial",
                Priority = "High",
                IsUserVisible = true,
                Scope = ComplianceScope.Organization
            }
        };

        _context.ComplianceControls.AddRange(controls);

        // Add assessment results
        var results = new List<ComplianceAssessmentResult>
        {
            new()
            {
                Id = 1,
                ReportId = report.Id,
                ControlId = "HIPAA-001",
                Status = "Implemented",
                Score = 95,
                Evidence = "Access control policy implemented",
                Findings = "Policy documented and enforced",
                Recommendations = "Continue monitoring compliance",
                AssessedAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        _context.ComplianceAssessmentResults.AddRange(results);
        _context.SaveChanges();
    }

    private async Task SeedHistoricalReports(string framework)
    {
        var historicalReports = new List<ComplianceReport>();

        for (int i = 1; i <= 5; i++)
        {
            historicalReports.Add(new ComplianceReport
            {
                Id = Guid.NewGuid().ToString(),
                Framework = framework,
                TotalControls = 17,
                ImplementedControls = 10 + i,
                FailedControls = 5 - i,
                ImplementationPercentage = 60 + (i * 5),
                RiskScore = 70 - (i * 5),
                Generated = DateTime.UtcNow.AddDays(-i * 7),
                NextReview = DateTime.UtcNow.AddDays(90),
                CreatedAt = DateTime.UtcNow.AddDays(-i * 7)
            });
        }

        _context.ComplianceReports.AddRange(historicalReports);
        await _context.SaveChangesAsync();
    }

    private ComplianceReportDocument CreateTestDocument()
    {
        return new ComplianceReportDocument
        {
            Title = "Test Compliance Report",
            Framework = "HIPAA",
            Format = ReportFormat.Json,
            Audience = ReportAudience.Technical,
            ExecutiveSummary = new ExecutiveSummarySection
            {
                Summary = "Test summary",
                OverallScore = 85.5,
                RiskLevel = "Medium"
            }
        };
    }

    private ComplianceReportDocument CreateTestDocumentWithControls()
    {
        var document = CreateTestDocument();
        document.ControlAssessment = new ControlAssessmentSection
        {
            Controls = new List<ControlAssessmentDetail>
            {
                new()
                {
                    ControlId = "AC-001",
                    ControlName = "Access Control",
                    Category = "Access Control",
                    Status = "Implemented",
                    Score = 95
                }
            }
        };
        return document;
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}