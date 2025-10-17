using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Castellan.Worker.Services;

namespace Castellan.Worker.Controllers;

/// <summary>
/// API endpoints for LLM evaluation and quality assurance.
/// Part of Phase 2 Week 4 AI Intelligence Upgrades (Automated Evaluations).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EvaluationController : ControllerBase
{
    private readonly LlmEvaluationService _evaluationService;
    private readonly ILogger<EvaluationController> _logger;

    private const string DefaultDatasetPath = "src/Castellan.Tests/testdata/golden-test-dataset.json";

    public EvaluationController(
        LlmEvaluationService evaluationService,
        ILogger<EvaluationController> logger)
    {
        _evaluationService = evaluationService ?? throw new ArgumentNullException(nameof(evaluationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Runs LLM evaluation against golden test dataset.
    /// </summary>
    /// <param name="request">Evaluation configuration</param>
    /// <returns>Detailed evaluation results with accuracy metrics</returns>
    [HttpPost("run")]
    public async Task<ActionResult<EvaluationResult>> RunEvaluation(
        [FromBody] RunEvaluationRequest? request,
        CancellationToken ct)
    {
        try
        {
            var datasetPath = string.IsNullOrWhiteSpace(request?.DatasetPath)
                ? DefaultDatasetPath
                : request.DatasetPath;

            _logger.LogInformation("Starting LLM evaluation from dataset: {DatasetPath}", datasetPath);

            var result = await _evaluationService.RunEvaluationAsync(datasetPath, ct);

            _logger.LogInformation(
                "Evaluation completed: {Accuracy:F2}% accuracy, {TestCases} test cases",
                result.OverallAccuracy * 100, result.TotalTestCases);

            return Ok(result);
        }
        catch (FileNotFoundException ex)
        {
            _logger.LogError(ex, "Golden dataset file not found");
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Evaluation failed");
            return StatusCode(500, new { error = "Evaluation failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Checks if current LLM configuration meets quality gates.
    /// Returns 200 if gates met, 417 (Expectation Failed) if not.
    /// </summary>
    [HttpGet("quality-gates")]
    public async Task<ActionResult<QualityGateResult>> CheckQualityGates(
        [FromQuery] string? datasetPath,
        CancellationToken ct)
    {
        try
        {
            var path = string.IsNullOrWhiteSpace(datasetPath) ? DefaultDatasetPath : datasetPath;

            _logger.LogInformation("Checking LLM quality gates against dataset: {DatasetPath}", path);

            var evaluation = await _evaluationService.RunEvaluationAsync(path, ct);
            var meetsGates = evaluation.MeetsQualityGates();

            var result = new QualityGateResult
            {
                MeetsGates = meetsGates,
                OverallAccuracy = evaluation.OverallAccuracy,
                RiskLevelAccuracy = evaluation.RiskLevelAccuracy,
                MitreRecall = evaluation.MitreRecall,
                RequiredOverallAccuracy = 0.95,
                RequiredRiskAccuracy = 0.90,
                RequiredMitreRecall = 0.80,
                TestCasesPassed = evaluation.SuccessfulEvaluations,
                TotalTestCases = evaluation.TotalTestCases
            };

            if (!meetsGates)
            {
                _logger.LogWarning(
                    "Quality gates NOT met: Overall={Accuracy:F2}% (need 95%), Risk={RiskAccuracy:F2}% (need 90%), MITRE={MitreRecall:F2}% (need 80%)",
                    evaluation.OverallAccuracy * 100, evaluation.RiskLevelAccuracy * 100, evaluation.MitreRecall * 100);

                return StatusCode(417, result); // 417 Expectation Failed
            }

            _logger.LogInformation("✅ Quality gates met: Overall={Accuracy:F2}%, Risk={RiskAccuracy:F2}%, MITRE={MitreRecall:F2}%",
                evaluation.OverallAccuracy * 100, evaluation.RiskLevelAccuracy * 100, evaluation.MitreRecall * 100);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quality gate check failed");
            return StatusCode(500, new { error = "Quality gate check failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Returns detailed results for a specific test case from last evaluation.
    /// Useful for debugging failures.
    /// </summary>
    [HttpGet("test-case/{testCaseId}")]
    public async Task<ActionResult<TestCaseResult>> GetTestCaseResult(
        string testCaseId,
        [FromQuery] string? datasetPath,
        CancellationToken ct)
    {
        try
        {
            var path = string.IsNullOrWhiteSpace(datasetPath) ? DefaultDatasetPath : datasetPath;

            var evaluation = await _evaluationService.RunEvaluationAsync(path, ct);
            var testCase = evaluation.TestCaseResults.FirstOrDefault(tc => tc.TestCaseId == testCaseId);

            if (testCase == null)
            {
                return NotFound(new { error = $"Test case '{testCaseId}' not found in dataset" });
            }

            return Ok(testCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve test case result for {TestCaseId}", testCaseId);
            return StatusCode(500, new { error = "Failed to retrieve test case result", details = ex.Message });
        }
    }
}

public class RunEvaluationRequest
{
    public string? DatasetPath { get; set; }
}

public class QualityGateResult
{
    public bool MeetsGates { get; set; }
    public double OverallAccuracy { get; set; }
    public double RiskLevelAccuracy { get; set; }
    public double MitreRecall { get; set; }
    public double RequiredOverallAccuracy { get; set; }
    public double RequiredRiskAccuracy { get; set; }
    public double RequiredMitreRecall { get; set; }
    public int TestCasesPassed { get; set; }
    public int TotalTestCases { get; set; }
}
