using Microsoft.Extensions.Options;
using Castellan.Worker.Services.Compliance;

namespace Castellan.Worker.Services.Compliance;

/// <summary>
/// Background service for automatic Application-scope compliance assessment
/// Runs assessments for CIS Controls, Windows Security Baselines, etc. that are hidden from users
/// Results are logged for internal monitoring of application security posture
/// </summary>
public class ApplicationComplianceBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ApplicationComplianceBackgroundService> _logger;
    private readonly TimeSpan _assessmentInterval;

    public ApplicationComplianceBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ApplicationComplianceBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _assessmentInterval = TimeSpan.FromHours(6); // Run every 6 hours
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Application Compliance Background Service started. Assessment interval: {Interval}", _assessmentInterval);

        // Wait a bit after startup before first assessment
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunApplicationComplianceAssessmentAsync();
                await Task.Delay(_assessmentInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when service is shutting down
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Application Compliance Background Service");
                // Wait shorter interval on error to retry sooner
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }

        _logger.LogInformation("Application Compliance Background Service stopped");
    }

    private async Task RunApplicationComplianceAssessmentAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var frameworkService = scope.ServiceProvider.GetRequiredService<IComplianceFrameworkService>();
        var assessmentService = scope.ServiceProvider.GetRequiredService<IComplianceAssessmentService>();

        try
        {
            _logger.LogInformation("Starting Application-scope compliance assessment cycle");

            // Get all Application-scope frameworks
            var applicationFrameworks = await frameworkService.GetApplicationFrameworksAsync();

            if (!applicationFrameworks.Any())
            {
                _logger.LogWarning("No Application-scope frameworks found for assessment");
                return;
            }

            _logger.LogInformation("Found {Count} Application-scope frameworks: {Frameworks}",
                applicationFrameworks.Count, string.Join(", ", applicationFrameworks));

            var assessmentResults = new List<ApplicationComplianceResult>();

            // Assess each Application-scope framework
            foreach (var framework in applicationFrameworks)
            {
                try
                {
                    var result = await AssessApplicationFrameworkAsync(assessmentService, frameworkService, framework);
                    assessmentResults.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error assessing Application framework: {Framework}", framework);
                    assessmentResults.Add(new ApplicationComplianceResult
                    {
                        Framework = framework,
                        AssessmentTime = DateTime.UtcNow,
                        Status = "Error",
                        Score = 0,
                        ErrorMessage = ex.Message
                    });
                }
            }

            // Log comprehensive assessment summary
            LogAssessmentSummary(assessmentResults);

            _logger.LogInformation("Application-scope compliance assessment cycle completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RunApplicationComplianceAssessmentAsync");
        }
    }

    private async Task<ApplicationComplianceResult> AssessApplicationFrameworkAsync(
        IComplianceAssessmentService assessmentService,
        IComplianceFrameworkService frameworkService,
        string framework)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("Assessing Application framework: {Framework}", framework);

        try
        {
            // Get controls for this Application framework
            var controls = await frameworkService.GetFrameworkControlsAsync(framework, userVisibleOnly: false);

            if (!controls.Any())
            {
                _logger.LogWarning("No controls found for Application framework: {Framework}", framework);
                return new ApplicationComplianceResult
                {
                    Framework = framework,
                    AssessmentTime = startTime,
                    Status = "NoControls",
                    Score = 0,
                    TotalControls = 0,
                    AssessedControls = 0
                };
            }

            // Calculate implementation percentage
            var implementationPercentage = await assessmentService.CalculateImplementationPercentageAsync(framework);
            var riskScore = await assessmentService.CalculateRiskScoreAsync(framework);

            var result = new ApplicationComplianceResult
            {
                Framework = framework,
                AssessmentTime = startTime,
                Status = "Completed",
                Score = implementationPercentage,
                RiskScore = riskScore,
                TotalControls = controls.Count,
                AssessedControls = controls.Count,
                Duration = DateTime.UtcNow - startTime
            };

            _logger.LogInformation("Application framework {Framework} assessment completed: {Score}% compliance, {RiskScore:F1} risk score",
                framework, implementationPercentage, riskScore);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assessing Application framework {Framework}", framework);
            return new ApplicationComplianceResult
            {
                Framework = framework,
                AssessmentTime = startTime,
                Status = "Error",
                Score = 0,
                ErrorMessage = ex.Message,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    private void LogAssessmentSummary(List<ApplicationComplianceResult> results)
    {
        var summary = new
        {
            TotalFrameworks = results.Count,
            SuccessfulAssessments = results.Count(r => r.Status == "Completed"),
            FailedAssessments = results.Count(r => r.Status == "Error"),
            AverageScore = results.Where(r => r.Status == "Completed").DefaultIfEmpty().Average(r => r?.Score ?? 0),
            AverageRiskScore = results.Where(r => r.Status == "Completed" && r.RiskScore.HasValue).DefaultIfEmpty().Average(r => r?.RiskScore ?? 0),
            TotalDuration = results.Sum(r => r.Duration?.TotalSeconds ?? 0),
            AssessmentTime = DateTime.UtcNow
        };

        _logger.LogInformation("=== APPLICATION COMPLIANCE ASSESSMENT SUMMARY ===");
        _logger.LogInformation("Assessment Time: {AssessmentTime:yyyy-MM-dd HH:mm:ss} UTC", summary.AssessmentTime);
        _logger.LogInformation("Total Frameworks Assessed: {Total}", summary.TotalFrameworks);
        _logger.LogInformation("Successful Assessments: {Successful}", summary.SuccessfulAssessments);
        _logger.LogInformation("Failed Assessments: {Failed}", summary.FailedAssessments);
        _logger.LogInformation("Average Application Security Score: {Score:F1}%", summary.AverageScore);
        _logger.LogInformation("Average Application Risk Score: {RiskScore:F1}/10", summary.AverageRiskScore);
        _logger.LogInformation("Total Assessment Duration: {Duration:F1} seconds", summary.TotalDuration);

        // Log individual framework results
        foreach (var result in results.OrderBy(r => r.Framework))
        {
            if (result.Status == "Completed")
            {
                _logger.LogInformation("Framework {Framework}: {Score}% compliance, {RiskScore:F1} risk, {Duration:F1}s",
                    result.Framework, result.Score, result.RiskScore ?? 0, result.Duration?.TotalSeconds ?? 0);
            }
            else
            {
                _logger.LogWarning("Framework {Framework}: {Status} - {Error}",
                    result.Framework, result.Status, result.ErrorMessage ?? "Unknown error");
            }
        }

        _logger.LogInformation("=== END APPLICATION COMPLIANCE SUMMARY ===");

        // Log compliance recommendations if scores are low
        var lowScoringFrameworks = results
            .Where(r => r.Status == "Completed" && r.Score < 80)
            .ToList();

        if (lowScoringFrameworks.Any())
        {
            _logger.LogWarning("APPLICATION SECURITY ATTENTION REQUIRED:");
            foreach (var framework in lowScoringFrameworks)
            {
                _logger.LogWarning("- {Framework}: {Score}% compliance (Target: 80%+)", framework.Framework, framework.Score);
            }
            _logger.LogWarning("Consider reviewing and improving application security controls for these frameworks");
        }
        else if (results.Any(r => r.Status == "Completed"))
        {
            _logger.LogInformation("APPLICATION SECURITY STATUS: All frameworks meeting compliance targets (80%+)");
        }
    }
}

public class ApplicationComplianceResult
{
    public string Framework { get; set; } = string.Empty;
    public DateTime AssessmentTime { get; set; }
    public string Status { get; set; } = string.Empty; // Completed, Error, NoControls
    public int Score { get; set; } // 0-100
    public float? RiskScore { get; set; } // 1-10 (lower is better)
    public int TotalControls { get; set; }
    public int AssessedControls { get; set; }
    public TimeSpan? Duration { get; set; }
    public string? ErrorMessage { get; set; }
}