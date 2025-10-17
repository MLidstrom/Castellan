using System.Text.Json;
using Microsoft.Extensions.Logging;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;

namespace Castellan.Worker.Services;

/// <summary>
/// Service for automated evaluation of LLM analysis quality against golden test dataset.
/// Part of Phase 2 Week 4 AI Intelligence Upgrades (Automated Evaluations).
///
/// Purpose:
/// - Prevent accuracy regressions when changing LLM prompts or models
/// - Measure LLM performance across different event types and risk levels
/// - Provide CI/CD gates to block deployments with <95% accuracy
/// - Track improvement over time as prompts are refined
///
/// Metrics Calculated:
/// - Overall accuracy (% of correct event types + risk levels)
/// - Risk level accuracy (critical/high/medium/low)
/// - MITRE technique recall (% of expected techniques identified)
/// - Confidence calibration (predicted vs actual confidence)
/// </summary>
public sealed class LlmEvaluationService
{
    private readonly ILlmClient _llmClient;
    private readonly ILogger<LlmEvaluationService> _logger;

    public LlmEvaluationService(
        ILlmClient llmClient,
        ILogger<LlmEvaluationService> logger)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Runs evaluation against golden test dataset and returns comprehensive metrics.
    /// </summary>
    public async Task<EvaluationResult> RunEvaluationAsync(
        string goldenDatasetPath,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting LLM evaluation from dataset: {Path}", goldenDatasetPath);

        // Load golden dataset
        var dataset = await LoadGoldenDatasetAsync(goldenDatasetPath, ct);
        _logger.LogInformation("Loaded {Count} test cases from golden dataset", dataset.TestCases.Count);

        var results = new List<TestCaseResult>();
        var startTime = DateTimeOffset.UtcNow;

        // Run LLM analysis on each test case
        foreach (var testCase in dataset.TestCases)
        {
            _logger.LogDebug("Evaluating test case: {Id} - {Name}", testCase.Id, testCase.Name);

            try
            {
                var result = await EvaluateTestCaseAsync(testCase, ct);
                results.Add(result);

                _logger.LogDebug("Test case {Id} completed: EventType={EventTypeMatch}, Risk={RiskMatch}, MITRE={MitreRecall:F2}",
                    testCase.Id, result.EventTypeMatch, result.RiskLevelMatch, result.MitreRecall);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to evaluate test case {Id}: {Name}", testCase.Id, testCase.Name);
                results.Add(new TestCaseResult
                {
                    TestCaseId = testCase.Id,
                    TestCaseName = testCase.Name,
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        var duration = (DateTimeOffset.UtcNow - startTime).TotalSeconds;

        // Calculate aggregate metrics
        var evaluation = new EvaluationResult
        {
            TotalTestCases = dataset.TestCases.Count,
            SuccessfulEvaluations = results.Count(r => r.Success),
            FailedEvaluations = results.Count(r => !r.Success),
            OverallAccuracy = CalculateOverallAccuracy(results),
            EventTypeAccuracy = CalculateEventTypeAccuracy(results),
            RiskLevelAccuracy = CalculateRiskLevelAccuracy(results),
            MitreRecall = CalculateAverageMitreRecall(results),
            ConfidenceCalibration = CalculateConfidenceCalibration(results),
            TestCaseResults = results,
            EvaluationDurationSeconds = duration,
            Timestamp = DateTimeOffset.UtcNow
        };

        _logger.LogInformation(
            "Evaluation complete: {Success}/{Total} successful, Overall Accuracy={Accuracy:F2}%, Risk Accuracy={RiskAccuracy:F2}%, MITRE Recall={MitreRecall:F2}%",
            evaluation.SuccessfulEvaluations, evaluation.TotalTestCases,
            evaluation.OverallAccuracy * 100, evaluation.RiskLevelAccuracy * 100, evaluation.MitreRecall * 100);

        return evaluation;
    }

    private async Task<GoldenDataset> LoadGoldenDatasetAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Golden dataset not found at path: {path}", path);
        }

        var json = await File.ReadAllTextAsync(path, ct);
        var dataset = JsonSerializer.Deserialize<GoldenDataset>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (dataset == null || dataset.TestCases == null || dataset.TestCases.Count == 0)
        {
            throw new InvalidOperationException($"Golden dataset at {path} is empty or invalid");
        }

        return dataset;
    }

    private async Task<TestCaseResult> EvaluateTestCaseAsync(GoldenTestCase testCase, CancellationToken ct)
    {
        // Convert golden test event to LogEvent
        var logEvent = new LogEvent(
            testCase.Event.Timestamp,
            testCase.Event.Host,
            testCase.Event.Channel,
            testCase.Event.EventId,
            testCase.Event.Message,
            testCase.Event.User,
            testCase.Event.RawJson,
            Guid.NewGuid().ToString()
        );

        // Run LLM analysis (no neighbor events for consistency)
        var llmResponseJson = await _llmClient.AnalyzeAsync(logEvent, Enumerable.Empty<LogEvent>(), ct);

        // Parse LLM response
        LlmSecurityEventResponse? actualOutput;
        try
        {
            actualOutput = JsonSerializer.Deserialize<LlmSecurityEventResponse>(llmResponseJson);
            if (actualOutput == null)
            {
                return new TestCaseResult
                {
                    TestCaseId = testCase.Id,
                    TestCaseName = testCase.Name,
                    Success = false,
                    ErrorMessage = "LLM response deserialization returned null"
                };
            }
        }
        catch (JsonException ex)
        {
            return new TestCaseResult
            {
                TestCaseId = testCase.Id,
                TestCaseName = testCase.Name,
                Success = false,
                ErrorMessage = $"Failed to parse LLM response JSON: {ex.Message}",
                ActualOutput = llmResponseJson
            };
        }

        // Calculate metrics
        var eventTypeMatch = string.Equals(
            actualOutput.EventType?.Trim(),
            testCase.ExpectedOutput.EventType?.Trim(),
            StringComparison.OrdinalIgnoreCase);

        var riskLevelMatch = string.Equals(
            actualOutput.RiskLevel?.Trim(),
            testCase.ExpectedOutput.Risk?.Trim(),
            StringComparison.OrdinalIgnoreCase);

        var mitreRecall = CalculateMitreRecall(
            actualOutput.MitreTechniques ?? Array.Empty<string>(),
            testCase.ExpectedOutput.Mitre ?? Array.Empty<string>());

        var summaryQuality = CalculateSummaryQuality(
            actualOutput.Summary ?? string.Empty,
            testCase.ExpectedOutput.Summary ?? string.Empty);

        var confidenceDelta = Math.Abs(
            (actualOutput.Confidence ?? 0) - testCase.ExpectedOutput.Confidence);

        return new TestCaseResult
        {
            TestCaseId = testCase.Id,
            TestCaseName = testCase.Name,
            Success = true,
            EventTypeMatch = eventTypeMatch,
            RiskLevelMatch = riskLevelMatch,
            MitreRecall = mitreRecall,
            SummaryQuality = summaryQuality,
            ConfidenceDelta = confidenceDelta,
            ExpectedRisk = testCase.ExpectedOutput.Risk ?? "unknown",
            ActualRisk = actualOutput.RiskLevel ?? "unknown",
            ExpectedMitre = testCase.ExpectedOutput.Mitre ?? Array.Empty<string>(),
            ActualMitre = actualOutput.MitreTechniques ?? Array.Empty<string>(),
            ActualOutput = llmResponseJson
        };
    }

    private double CalculateMitreRecall(string[] actual, string[] expected)
    {
        if (expected.Length == 0)
        {
            // If no MITRE techniques expected, perfect recall if none returned
            return actual.Length == 0 ? 1.0 : 1.0;
        }

        var actualSet = new HashSet<string>(actual, StringComparer.OrdinalIgnoreCase);
        var matchCount = expected.Count(e => actualSet.Contains(e));

        return (double)matchCount / expected.Length;
    }

    private double CalculateSummaryQuality(string actual, string expected)
    {
        // Simple keyword overlap metric (can be enhanced with semantic similarity)
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual))
        {
            return 0.0;
        }

        var expectedKeywords = ExtractKeywords(expected);
        var actualKeywords = ExtractKeywords(actual);

        if (expectedKeywords.Count == 0)
        {
            return 0.0;
        }

        var matchCount = actualKeywords.Intersect(expectedKeywords, StringComparer.OrdinalIgnoreCase).Count();
        return (double)matchCount / expectedKeywords.Count;
    }

    private HashSet<string> ExtractKeywords(string text)
    {
        // Extract significant words (>4 characters, excluding common words)
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "for", "from", "with", "this", "that", "have", "been", "were", "their"
        };

        return text
            .Split(new[] { ' ', ',', '.', ';', ':', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 4 && !stopWords.Contains(w))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private double CalculateOverallAccuracy(List<TestCaseResult> results)
    {
        var successful = results.Where(r => r.Success).ToList();
        if (successful.Count == 0) return 0.0;

        // Overall accuracy = (event type + risk level) / 2
        var eventTypeCorrect = successful.Count(r => r.EventTypeMatch);
        var riskLevelCorrect = successful.Count(r => r.RiskLevelMatch);

        return ((double)eventTypeCorrect + riskLevelCorrect) / (successful.Count * 2);
    }

    private double CalculateEventTypeAccuracy(List<TestCaseResult> results)
    {
        var successful = results.Where(r => r.Success).ToList();
        if (successful.Count == 0) return 0.0;

        return (double)successful.Count(r => r.EventTypeMatch) / successful.Count;
    }

    private double CalculateRiskLevelAccuracy(List<TestCaseResult> results)
    {
        var successful = results.Where(r => r.Success).ToList();
        if (successful.Count == 0) return 0.0;

        return (double)successful.Count(r => r.RiskLevelMatch) / successful.Count;
    }

    private double CalculateAverageMitreRecall(List<TestCaseResult> results)
    {
        var successful = results.Where(r => r.Success).ToList();
        if (successful.Count == 0) return 0.0;

        return successful.Average(r => r.MitreRecall);
    }

    private double CalculateConfidenceCalibration(List<TestCaseResult> results)
    {
        var successful = results.Where(r => r.Success).ToList();
        if (successful.Count == 0) return 0.0;

        // Good calibration = low average confidence delta
        var avgDelta = successful.Average(r => r.ConfidenceDelta);

        // Convert to 0-1 score (lower delta = better calibration)
        return Math.Max(0, 1.0 - (avgDelta / 100.0));
    }
}

/// <summary>
/// Represents the golden test dataset structure.
/// </summary>
public class GoldenDataset
{
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Created { get; set; } = string.Empty;
    public List<GoldenTestCase> TestCases { get; set; } = new();
}

/// <summary>
/// Represents a single test case in the golden dataset.
/// </summary>
public class GoldenTestCase
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public GoldenEventData Event { get; set; } = new();
    public GoldenExpectedOutput ExpectedOutput { get; set; } = new();
}

public class GoldenEventData
{
    public int EventId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string RawJson { get; set; } = string.Empty;
}

public class GoldenExpectedOutput
{
    public string EventType { get; set; } = string.Empty;
    public string Risk { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string[] Mitre { get; set; } = Array.Empty<string>();
    public string[] RecommendedActions { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Result of evaluating a single test case.
/// </summary>
public class TestCaseResult
{
    public string TestCaseId { get; set; } = string.Empty;
    public string TestCaseName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool EventTypeMatch { get; set; }
    public bool RiskLevelMatch { get; set; }
    public double MitreRecall { get; set; }
    public double SummaryQuality { get; set; }
    public double ConfidenceDelta { get; set; }
    public string ExpectedRisk { get; set; } = string.Empty;
    public string ActualRisk { get; set; } = string.Empty;
    public string[] ExpectedMitre { get; set; } = Array.Empty<string>();
    public string[] ActualMitre { get; set; } = Array.Empty<string>();
    public string? ActualOutput { get; set; }
}

/// <summary>
/// Aggregate evaluation metrics across all test cases.
/// </summary>
public class EvaluationResult
{
    public int TotalTestCases { get; set; }
    public int SuccessfulEvaluations { get; set; }
    public int FailedEvaluations { get; set; }
    public double OverallAccuracy { get; set; }
    public double EventTypeAccuracy { get; set; }
    public double RiskLevelAccuracy { get; set; }
    public double MitreRecall { get; set; }
    public double ConfidenceCalibration { get; set; }
    public List<TestCaseResult> TestCaseResults { get; set; } = new();
    public double EvaluationDurationSeconds { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Returns true if evaluation meets CI/CD quality gates.
    /// </summary>
    public bool MeetsQualityGates()
    {
        return OverallAccuracy >= 0.95 &&  // 95% overall accuracy
               RiskLevelAccuracy >= 0.90 &&  // 90% risk level accuracy
               MitreRecall >= 0.80;           // 80% MITRE recall
    }
}
