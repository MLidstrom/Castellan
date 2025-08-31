using System.Threading.Channels;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Castellan.Worker.Services;
using Castellan.Worker.Configuration;

namespace Castellan.Worker;

public sealed class Pipeline(
    IEnumerable<ILogCollector> collectors,
    IEmbedder embedder,
    IVectorStore store,
    ILlmClient llm,
    SecurityEventDetector securityDetector,
    RulesEngine rulesEngine,
    IIPEnrichmentService ipEnrichmentService,
    IOptions<AlertOptions> alertOptions,
    IOptions<NotificationOptions> notificationOptions,
    INotificationService notificationService,
    IPerformanceMonitor performanceMonitor,
    ISecurityEventStore securityEventStore,
    IAutomatedResponseService automatedResponseService,
    ILogger<Pipeline> log
) : BackgroundService
{
    private readonly IOptions<NotificationOptions> _notificationOptions = notificationOptions;
    private readonly IIPEnrichmentService _ipEnrichmentService = ipEnrichmentService;
    private readonly IAutomatedResponseService _automatedResponseService = automatedResponseService;
    
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await store.EnsureCollectionAsync(ct);

        // Check if we have 24 hours of data, and backfill if needed
        await CheckAndBackfill24HoursData(ct);

        // Start background cleanup task that runs every hour
        var cleanupTask = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromHours(1), ct); // Wait 1 hour
                    if (!ct.IsCancellationRequested)
                    {
                        log.LogInformation("Starting periodic cleanup of vectors older than 24 hours...");
                        await store.DeleteVectorsOlderThan24HoursAsync(ct);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Error during periodic vector cleanup");
                }
            }
        }, ct);

        // Merge all collectors into a single stream
        var streams = collectors.Select(c => c.CollectAsync(ct));
        var eventCount = 0;
        var queueDepth = 0;
        
        await foreach (var e in Merge(streams, ct))
        {
            var processingStartTime = DateTimeOffset.UtcNow;
            try
            {
                eventCount++;
                queueDepth++; // Simplified queue depth tracking
                
                // M4: Enhanced analysis with correlation and fusion
                var securityEvent = await AnalyzeEventWithCorrelation(e, ct);
                
                if (securityEvent != null)
                {
                    await ProcessSecurityEvent(securityEvent, ct);
                }
                
                queueDepth--;
                
                // Record pipeline performance metrics
                var processingTime = (DateTimeOffset.UtcNow - processingStartTime).TotalMilliseconds;
                performanceMonitor.RecordPipelineMetrics(processingTime, 1, queueDepth);
            }
            catch (Exception ex)
            {
                queueDepth--;
                log.LogError(ex, "Pipeline error for event {EventId}", e.EventId);
                
                // Record pipeline error metrics
                var processingTime = (DateTimeOffset.UtcNow - processingStartTime).TotalMilliseconds;
                performanceMonitor.RecordPipelineMetrics(processingTime, 0, queueDepth, ex.Message);
            }
        }

        // Wait for cleanup task to complete
        await cleanupTask;
    }

    private async Task CheckAndBackfill24HoursData(CancellationToken ct)
    {
        try
        {
            log.LogInformation("Checking if vector store has 24 hours of historical data...");
            
            var has24HoursData = await store.Has24HoursOfDataAsync(ct);
            
            if (has24HoursData)
            {
                log.LogInformation("Vector store already has 24 hours of historical data. Skipping backfill.");
                return;
            }

            log.LogInformation("Vector store does not have 24 hours of data. Starting historical backfill...");
            
            // Get the EVTX collector for historical backfill
            var evtxCollector = collectors.OfType<Collectors.EvtxCollector>().FirstOrDefault();
            if (evtxCollector == null)
            {
                log.LogWarning("No EVTX collector found. Cannot perform historical backfill.");
                return;
            }

            var backfillCount = 0;
            await foreach (var historicalEvent in evtxCollector.CollectHistoricalAsync(ct))
            {
                if (ct.IsCancellationRequested) break;
                
                try
                {
                    // Process historical event (simplified analysis for backfill)
                    var text = $"{historicalEvent.Channel} {historicalEvent.EventId} {historicalEvent.Level} {historicalEvent.User}\n{historicalEvent.Message}";
                    var emb = await embedder.EmbedAsync(text, ct);
                    await store.UpsertAsync(historicalEvent, emb, ct);
                    backfillCount++;
                    
                    if (backfillCount % 100 == 0)
                    {
                        log.LogInformation("Historical backfill progress: {Count} events processed", backfillCount);
                    }
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "Error processing historical event {EventId} during backfill", historicalEvent.EventId);
                }
            }
            
            log.LogInformation("Historical backfill completed. Processed {Count} events.", backfillCount);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error during 24-hour data check and backfill");
        }
    }

    private async Task<SecurityEvent?> AnalyzeEventWithCorrelation(LogEvent logEvent, CancellationToken ct)
    {
        // Extract and enrich IP addresses from the event
        var enrichmentData = await EnrichEventIPs(logEvent, ct);
        
        // First, try deterministic security event detection
        var deterministicEvent = securityDetector.DetectSecurityEvent(logEvent);
        
        SecurityEvent? llmEvent = null;
        
        // If no deterministic event, or for additional analysis, try LLM
        if (deterministicEvent == null || deterministicEvent.RiskLevel == "low")
        {
            try
            {
                var text = $"{logEvent.Channel} {logEvent.EventId} {logEvent.Level} {logEvent.User}\n{logEvent.Message}";
                
                // Measure embedding time
                var embeddingStartTime = DateTimeOffset.UtcNow;
                var emb = await embedder.EmbedAsync(text, ct);
                var embeddingTime = (DateTimeOffset.UtcNow - embeddingStartTime).TotalMilliseconds;
                
                log.LogDebug("Pipeline: embedding length={emb.Length} for event {EventId}", emb.Length);
                
                // Measure upsert time
                var upsertStartTime = DateTimeOffset.UtcNow;
                await store.UpsertAsync(logEvent, emb, ct);
                var upsertTime = (DateTimeOffset.UtcNow - upsertStartTime).TotalMilliseconds;
                
                // Measure search time
                var searchStartTime = DateTimeOffset.UtcNow;
                var neighbors = await store.SearchAsync(emb, k: 8, ct);
                var searchTime = (DateTimeOffset.UtcNow - searchStartTime).TotalMilliseconds;
                
                // Record vector store metrics
                performanceMonitor.RecordVectorStoreMetrics(embeddingTime, upsertTime, searchTime, 1);
                
                // Measure LLM analysis time
                var llmStartTime = DateTimeOffset.UtcNow;
                var analysis = await llm.AnalyzeAsync(logEvent, neighbors.Select(n => n.evt), ct);
                var llmTime = (DateTimeOffset.UtcNow - llmStartTime).TotalMilliseconds;
                
                // Record LLM metrics (provider/model info would need to be extracted from llm)
                performanceMonitor.RecordLlmMetrics("Ollama", "llama3.1", llmTime, 0, true);
                
                // Create security event from LLM response
                llmEvent = SecurityEvent.CreateFromLlmResponse(logEvent, analysis);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "LLM analysis failed for event {EventId}, continuing with deterministic analysis only", logEvent.EventId);
                
                // Record failed vector store or LLM metrics
                performanceMonitor.RecordVectorStoreMetrics(error: ex.Message);
                performanceMonitor.RecordLlmMetrics("Ollama", "llama3.1", 0, 0, false);
            }
        }

        // M4: Use rules engine for correlation and fusion
                    log.LogInformation("Before rules engine: deterministicEvent={DeterministicEvent}, llmEvent={LlmEvent}", 
            deterministicEvent?.RiskLevel ?? "null", llmEvent?.RiskLevel ?? "null");
        
        var fusedEvent = rulesEngine.AnalyzeWithCorrelation(logEvent, deterministicEvent, llmEvent);
        
        // Add IP enrichment data to the fused event
        if (fusedEvent != null && !string.IsNullOrEmpty(enrichmentData))
        {
            fusedEvent = AddIPEnrichmentToEvent(fusedEvent, enrichmentData);
        }
        
                    log.LogInformation("After rules engine: fusedEvent={FusedEvent}", 
            fusedEvent?.RiskLevel ?? "null");
        
        if (fusedEvent != null)
        {
            var detectionType = fusedEvent.IsDeterministic ? "Deterministic" : 
                               fusedEvent.IsCorrelationBased ? "Correlation" :
                               fusedEvent.IsEnhanced ? "Enhanced" : "LLM";
            
            log.LogInformation("Security event detected: {EventId} -> {EventType} ({RiskLevel}) [{DetectionType}] corr={CorrelationScore:F2} burst={BurstScore:F2} anomaly={AnomalyScore:F2}", 
                logEvent.EventId, fusedEvent.EventType, fusedEvent.RiskLevel, detectionType,
                fusedEvent.CorrelationScore, fusedEvent.BurstScore, fusedEvent.AnomalyScore);
            
            // Record security detection metrics
            performanceMonitor.RecordSecurityDetection(
                fusedEvent.EventType.ToString(),
                fusedEvent.RiskLevel,
                fusedEvent.Confidence,
                fusedEvent.IsDeterministic,
                fusedEvent.IsCorrelationBased
            );
        }
        else
        {
            log.LogWarning("Rules engine returned null for event {EventId} - no security event will be processed", logEvent.EventId);
        }

        return fusedEvent;
    }

    private async Task<string?> EnrichEventIPs(LogEvent logEvent, CancellationToken ct)
    {
        try
        {
            // Extract IP addresses from the event message
            var ipAddresses = IPExtractor.ExtractAuthenticationIPs(logEvent.Message, logEvent.EventId);
            
            if (!ipAddresses.Any())
            {
                return null;
            }

            // Get the primary IP for enrichment
            var primaryIP = IPExtractor.GetPrimaryIP(ipAddresses);
            if (string.IsNullOrEmpty(primaryIP))
            {
                return null;
            }

            log.LogDebug("🌍 Enriching IP {IP} from event {EventId}", primaryIP, logEvent.EventId);
            
            // Enrich the primary IP
            var enrichmentResult = await _ipEnrichmentService.EnrichAsync(primaryIP, ct);
            
            if (enrichmentResult.IsEnriched)
            {
                var enrichmentJson = JsonSerializer.Serialize(new
                {
                    ip = enrichmentResult.IPAddress,
                    country = enrichmentResult.Country,
                    countryCode = enrichmentResult.CountryCode,
                    city = enrichmentResult.City,
                    latitude = enrichmentResult.Latitude,
                    longitude = enrichmentResult.Longitude,
                    asn = enrichmentResult.ASN,
                    asnOrganization = enrichmentResult.ASNOrganization,
                    isHighRisk = enrichmentResult.IsHighRisk,
                    riskFactors = enrichmentResult.RiskFactors,
                    isPrivate = enrichmentResult.IsPrivate
                });
                
                log.LogInformation("🌍 IP enrichment successful: {IP} -> {Country} (ASN: {ASN}) Risk: {IsHighRisk}", 
                    primaryIP, enrichmentResult.Country, enrichmentResult.ASN, enrichmentResult.IsHighRisk);
                
                return enrichmentJson;
            }
            else
            {
                log.LogDebug("🌍 IP enrichment failed for {IP}: {Error}", primaryIP, enrichmentResult.Error);
                return null;
            }
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Error during IP enrichment for event {EventId}", logEvent.EventId);
            return null;
        }
    }

    private static SecurityEvent AddIPEnrichmentToEvent(SecurityEvent originalEvent, string enrichmentData)
    {
        // Create a new SecurityEvent with IP enrichment data
        return new SecurityEvent
        {
            OriginalEvent = originalEvent.OriginalEvent,
            EventType = originalEvent.EventType,
            RiskLevel = originalEvent.RiskLevel,
            Confidence = originalEvent.Confidence,
            Summary = originalEvent.Summary,
            MitreTechniques = originalEvent.MitreTechniques,
            RecommendedActions = originalEvent.RecommendedActions,
            IsDeterministic = originalEvent.IsDeterministic,
            EnrichmentData = enrichmentData, // Add the IP enrichment data
            CorrelationScore = originalEvent.CorrelationScore,
            BurstScore = originalEvent.BurstScore,
            AnomalyScore = originalEvent.AnomalyScore,
            IsCorrelationBased = originalEvent.IsCorrelationBased,
            IsEnhanced = originalEvent.IsEnhanced
        };
    }

    private async Task ProcessSecurityEvent(SecurityEvent securityEvent, CancellationToken ct)
    {
        var e = securityEvent.OriginalEvent;
        
        // Store the security event for API access
        securityEventStore.AddSecurityEvent(securityEvent);
        
                    log.LogInformation("Processing security event: Risk={RiskLevel}, EventId={EventId}, Channel={Channel}", 
            securityEvent.RiskLevel, e.EventId, e.Channel);
        
        // Check if we should alert based on risk level threshold
        var shouldAlert = alertOptions.Value.ShouldAlert(securityEvent.RiskLevel);
        var enableConsole = alertOptions.Value.EnableConsoleAlerts;
        
                    log.LogInformation("Alert check: ShouldAlert={ShouldAlert}, EnableConsole={EnableConsole}, MinRiskLevel={MinRiskLevel}", 
            shouldAlert, enableConsole, alertOptions.Value.MinRiskLevel);
        
        if (shouldAlert && enableConsole)
        {
            var alertColor = securityEvent.RiskLevel switch
            {
                "critical" => ConsoleColor.Red,
                "high" => ConsoleColor.Yellow,
                "medium" => ConsoleColor.Cyan,
                "low" => ConsoleColor.Green,
                _ => ConsoleColor.Gray
            };
            
            Console.ForegroundColor = alertColor;
            Console.WriteLine($"🚨 ALERT [{securityEvent.RiskLevel.ToUpper()}] [{securityEvent.Confidence}%] [{e.Channel}/{e.EventId}] {e.Time:o}");
            Console.WriteLine($"   Type: {securityEvent.EventType}");
            Console.WriteLine($"   Summary: {securityEvent.Summary}");
            
            // Display IP enrichment data if available
            if (!string.IsNullOrEmpty(securityEvent.EnrichmentData))
            {
                try
                {
                    var enrichment = JsonSerializer.Deserialize<JsonElement>(securityEvent.EnrichmentData);
                    if (enrichment.TryGetProperty("ip", out var ipProp))
                    {
                        var ip = ipProp.GetString();
                        var country = enrichment.TryGetProperty("country", out var countryProp) ? countryProp.GetString() : "Unknown";
                        var isHighRisk = enrichment.TryGetProperty("isHighRisk", out var riskProp) && riskProp.GetBoolean();
                        var asn = enrichment.TryGetProperty("asn", out var asnProp) && asnProp.TryGetInt32(out var asnValue) ? asnValue : (int?)null;
                        
                        var riskIndicator = isHighRisk ? "🔴" : "🟢";
                        Console.WriteLine($"   IP: {ip} -> {country} {riskIndicator} {(asn.HasValue ? $"AS{asn}" : "")}");
                        
                        if (enrichment.TryGetProperty("riskFactors", out var factorsProp) && factorsProp.GetArrayLength() > 0)
                        {
                            var factors = factorsProp.EnumerateArray().Select(f => f.GetString()).Where(f => !string.IsNullOrEmpty(f));
                            Console.WriteLine($"   Risk Factors: {string.Join(", ", factors)}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.LogDebug(ex, "Failed to parse IP enrichment data for display");
                }
            }
            
            // M4: Display correlation scores if available
            if (securityEvent.CorrelationScore > 0 || securityEvent.BurstScore > 0 || securityEvent.AnomalyScore > 0)
            {
                Console.WriteLine($"   Correlation: {securityEvent.CorrelationScore:F2} | Burst: {securityEvent.BurstScore:F2} | Anomaly: {securityEvent.AnomalyScore:F2}");
            }
            
            if (securityEvent.MitreTechniques.Length > 0)
            {
                Console.WriteLine($"   MITRE: {string.Join(", ", securityEvent.MitreTechniques)}");
            }
            if (securityEvent.RecommendedActions.Length > 0)
            {
                Console.WriteLine($"   Actions: {string.Join("; ", securityEvent.RecommendedActions)}");
            }
            
            var detectionType = securityEvent.IsDeterministic ? "Deterministic Rule" :
                               securityEvent.IsCorrelationBased ? "Correlation Engine" :
                               securityEvent.IsEnhanced ? "Enhanced Analysis" : "LLM Analysis";
            Console.WriteLine($"   Detection: {detectionType}");
            
            Console.ResetColor();
            
            // Show desktop notification
            var notificationTitle = $"Castellan Security Alert - {securityEvent.RiskLevel.ToUpper()}";
            var notificationMessage = $"{securityEvent.EventType}\n{securityEvent.Summary}";
            
            // Add IP enrichment data to notification if available and enabled
            if (!string.IsNullOrEmpty(securityEvent.EnrichmentData) && _notificationOptions.Value.ShowIPEnrichment)
            {
                try
                {
                    var enrichment = JsonSerializer.Deserialize<JsonElement>(securityEvent.EnrichmentData);
                    if (enrichment.TryGetProperty("ip", out var ipProp))
                    {
                        var ip = ipProp.GetString();
                        var country = enrichment.TryGetProperty("country", out var countryProp) ? countryProp.GetString() : "Unknown";
                        var city = enrichment.TryGetProperty("city", out var cityProp) ? cityProp.GetString() : null;
                        var asn = enrichment.TryGetProperty("asn", out var asnProp) && asnProp.TryGetInt32(out var asnValue) ? asnValue : (int?)null;
                        var asnOrg = enrichment.TryGetProperty("asnOrganization", out var asnOrgProp) ? asnOrgProp.GetString() : null;
                        var isHighRisk = enrichment.TryGetProperty("isHighRisk", out var riskProp) && riskProp.GetBoolean();
                        
                        var riskIndicator = isHighRisk ? "🔴" : "🟢";
                        var location = city != null ? $"{city}, {country}" : country;
                        var asnInfo = asn.HasValue ? $"AS{asn}" : "";
                        var asnDetails = asnOrg != null ? $" ({asnOrg})" : "";
                        
                        notificationMessage += $"\nIP: {ip} → {location} {riskIndicator} {asnInfo}{asnDetails}";
                        
                        if (enrichment.TryGetProperty("riskFactors", out var factorsProp) && factorsProp.GetArrayLength() > 0)
                        {
                            var factors = factorsProp.EnumerateArray().Select(f => f.GetString()).Where(f => !string.IsNullOrEmpty(f));
                            if (factors.Any())
                            {
                                notificationMessage += $"\nRisk Factors: {string.Join(", ", factors)}";
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.LogDebug(ex, "Failed to parse IP enrichment data for notification");
                }
            }
            
            if (securityEvent.CorrelationScore > 0 || securityEvent.BurstScore > 0 || securityEvent.AnomalyScore > 0)
            {
                notificationMessage += $"\nCorrelation: {securityEvent.CorrelationScore:F2} | Burst: {securityEvent.BurstScore:F2} | Anomaly: {securityEvent.AnomalyScore:F2}";
            }
            
            log.LogInformation("🔔 Calling notification service with:");
            log.LogInformation("   - Title: {Title}", notificationTitle);
            log.LogInformation("   - Message: {Message}", notificationMessage);
            log.LogInformation("   - Risk Level: {RiskLevel}", securityEvent.RiskLevel);
            
            // Measure notification delivery time
            var notificationStartTime = DateTimeOffset.UtcNow;
            try
            {
                await notificationService.ShowNotificationAsync(notificationTitle, notificationMessage, securityEvent.RiskLevel);
                
                var notificationTime = (DateTimeOffset.UtcNow - notificationStartTime).TotalMilliseconds;
                performanceMonitor.RecordNotificationMetrics("Desktop", securityEvent.RiskLevel, notificationTime, true);
                
                log.LogInformation("🔔 Notification service call completed");
            }
            catch (Exception ex)
            {
                var notificationTime = (DateTimeOffset.UtcNow - notificationStartTime).TotalMilliseconds;
                performanceMonitor.RecordNotificationMetrics("Desktop", securityEvent.RiskLevel, notificationTime, false);
                
                log.LogError(ex, "Failed to send desktop notification");
                throw;
            }
            
            // Log to file if enabled
            if (alertOptions.Value.EnableFileLogging)
            {
                log.LogWarning("SECURITY ALERT: [{RiskLevel}] [{Confidence}%] [{Channel}/{EventId}] {EventType} - {Summary} | MITRE: {MitreTechniques} | Actions: {Actions} | Detection: {DetectionType} | Corr: {CorrelationScore:F2} Burst: {BurstScore:F2} Anomaly: {AnomalyScore:F2}", 
                    securityEvent.RiskLevel.ToUpper(), securityEvent.Confidence, e.Channel, e.EventId, securityEvent.EventType, securityEvent.Summary, 
                    string.Join(",", securityEvent.MitreTechniques), string.Join(";", securityEvent.RecommendedActions), 
                    detectionType, securityEvent.CorrelationScore, securityEvent.BurstScore, securityEvent.AnomalyScore);
            }

            // Execute automated response if enabled and risk level meets threshold
            try
            {
                var isResponseEnabled = await _automatedResponseService.IsResponseEnabledAsync();
                if (isResponseEnabled && (securityEvent.RiskLevel == "high" || securityEvent.RiskLevel == "critical"))
                {
                    log.LogInformation("🤖 Executing automated response for {EventType} with risk level {RiskLevel}", 
                        securityEvent.EventType, securityEvent.RiskLevel);
                    
                    await _automatedResponseService.ExecuteResponseAsync(securityEvent);
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "🤖 Failed to execute automated response for event {EventId}", e.EventId);
            }
        }
        else
        {
            // Log non-alert events at debug level
            log.LogDebug("Security event processed: [{RiskLevel}] [{Confidence}%] [{Channel}/{EventId}] {EventType} - {Summary} | Corr: {CorrelationScore:F2} Burst: {BurstScore:F2} Anomaly: {AnomalyScore:F2}", 
                securityEvent.RiskLevel.ToUpper(), securityEvent.Confidence, e.Channel, e.EventId, securityEvent.EventType, securityEvent.Summary,
                securityEvent.CorrelationScore, securityEvent.BurstScore, securityEvent.AnomalyScore);
        }
    }

    private async IAsyncEnumerable<T> Merge<T>(IEnumerable<IAsyncEnumerable<T>> sources, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateUnbounded<T>();
        var tasks = sources.Select(async src =>
        {
            try
            {
                await foreach (var item in src.WithCancellation(ct))
                    await channel.Writer.WriteAsync(item, ct);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                // No logging needed for normal cancellation
            }
            catch (Exception ex)
            {
                // Log critical errors that should not be silently ignored
                log.LogError(ex, "Critical error in Merge method for source stream");
                
                // Re-throw critical exceptions to prevent silent failures
                throw;
            }
        }).ToArray();

        _ = Task.WhenAll(tasks).ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception != null)
            {
                // Log any unhandled exceptions from the tasks
                log.LogError(t.Exception, "Unhandled exception in Merge tasks");
            }
            channel.Writer.TryComplete();
        });

        while (await channel.Reader.WaitToReadAsync(ct))
        {
            while (channel.Reader.TryRead(out var item))
                yield return item;
        }
    }
}

