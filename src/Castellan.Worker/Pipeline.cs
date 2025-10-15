using System.Threading.Channels;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
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
    IOptionsMonitor<PipelineOptions> pipelineOptions,
    INotificationService notificationService,
    IPerformanceMonitor performanceMonitor,
    ISecurityEventStore securityEventStore,
    IAutomatedResponseService automatedResponseService,
    IMalwareScanService malwareScanService,
    IMalwareRuleStore malwareRuleStore,
    IOptionsMonitor<MalwareScanningOptions> yaraScanningOptions,
    EventIgnorePatternService ignorePatternService,
    ILogger<Pipeline> log
) : BackgroundService
{
    private readonly IOptions<NotificationOptions> _notificationOptions = notificationOptions;
    private readonly IOptionsMonitor<PipelineOptions> _pipelineOptions = pipelineOptions;
    private readonly IIPEnrichmentService _ipEnrichmentService = ipEnrichmentService;
    private readonly IAutomatedResponseService _automatedResponseService = automatedResponseService;
    private readonly IMalwareScanService _malwareScanService = malwareScanService;
    private readonly IMalwareRuleStore _malwareRuleStore = malwareRuleStore;
    private readonly IOptionsMonitor<MalwareScanningOptions> _yaraScanningOptions = yaraScanningOptions;
    
    // Semaphore-based throttling for pipeline processing
    private SemaphoreSlim? _processingSemaphore;
    private readonly object _semaphoreLock = new object();
    
    // Throttling metrics
#pragma warning disable CS0649 // Field is never assigned to - these are infrastructure for semaphore throttling implementation
    private long _semaphoreQueueCount;
    private long _semaphoreWaitTimeTotal;
    private long _semaphoreAcquisitions;
    private long _semaphoreTimeouts;
#pragma warning restore CS0649
    
    // Performance tracking
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;
    private double _baselineEventsPerSecond = 0;
    
    // Vector batch processing
    private readonly List<(LogEvent logEvent, float[] embedding)> _vectorBuffer = new();
    private readonly object _vectorBufferLock = new object();
    private System.Threading.Timer? _batchFlushTimer;
    private volatile bool _isFlushingBatch = false;
    private long _totalVectorsBatched = 0;
    private long _totalBatchFlushes = 0;
    
    /// <summary>
    /// Initialize or update the processing semaphore based on current configuration
    /// </summary>
    private void InitializeSemaphore()
    {
        var options = _pipelineOptions.CurrentValue;
        
        if (!options.EnableSemaphoreThrottling)
        {
            // Disable semaphore if not enabled
            lock (_semaphoreLock)
            {
                _processingSemaphore?.Dispose();
                _processingSemaphore = null;
            }
            return;
        }
        
        lock (_semaphoreLock)
        {
            // Dispose existing semaphore if we need to recreate it
            // (We can't easily check if the limit changed, so we'll recreate if needed)
            if (_processingSemaphore == null)
            {
                // Will be created below
            }
            
            // Create new semaphore if needed
            if (_processingSemaphore == null)
            {
                _processingSemaphore = new SemaphoreSlim(options.MaxConcurrentTasks, options.MaxConcurrentTasks);
                log.LogInformation("Pipeline semaphore initialized with {MaxConcurrentTasks} concurrent tasks", options.MaxConcurrentTasks);
            }
        }
    }
    
    /// <summary>
    /// Acquire semaphore permission with timeout and metrics tracking
    /// </summary>
    private async Task<bool> TryAcquireSemaphoreAsync(CancellationToken ct)
    {
        // If semaphore is disabled, return true but indicate no acquisition needed
        var semaphore = _processingSemaphore;
        if (semaphore == null)
        {
            return true;
        }

        // Use proper async WaitAsync with timeout (Sprint 2 Phase 3 optimization)
        var timeout = TimeSpan.FromMilliseconds(_pipelineOptions.CurrentValue.SemaphoreTimeoutMs);
        try
        {
            return await semaphore.WaitAsync(timeout, ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
    
    /// <summary>
    /// Release semaphore permission
    /// </summary>
    private void ReleaseSemaphore()
    {
        var semaphore = _processingSemaphore;
        if (semaphore != null)
        {
            semaphore.Release();
        }
    }
    
    /// <summary>
    /// Get current semaphore queue depth for metrics
    /// </summary>
    private int GetCurrentSemaphoreQueueDepth()
    {
        var semaphore = _processingSemaphore;
        if (semaphore == null) return 0;
        
        var options = _pipelineOptions.CurrentValue;
        return Math.Max(0, options.MaxConcurrentTasks - semaphore.CurrentCount);
    }
    
    /// <summary>
    /// Get current throttling metrics for performance monitoring
    /// </summary>
    public (long QueueCount, long Acquisitions, long Timeouts, double AvgWaitTimeMs) GetThrottlingMetrics()
    {
        var acquisitions = _semaphoreAcquisitions;
        var avgWaitTime = acquisitions > 0 ? (double)_semaphoreWaitTimeTotal / acquisitions : 0;
        
        return (_semaphoreQueueCount, acquisitions, _semaphoreTimeouts, avgWaitTime);
    }
    
    /// <summary>
    /// Calculate current events per second
    /// </summary>
    private double CalculateCurrentEventsPerSecond(int totalEvents, DateTimeOffset startTime)
    {
        var elapsedSeconds = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
        if (elapsedSeconds <= 0) return 0;
        
        return totalEvents / elapsedSeconds;
    }
    
    /// <summary>
    /// Calculate throughput improvement percentage vs baseline
    /// </summary>
    private double CalculateThroughputImprovement(double currentEventsPerSecond)
    {
        if (_baselineEventsPerSecond == 0)
        {
            _baselineEventsPerSecond = currentEventsPerSecond;
            return 0; // No improvement to calculate on first measurement
        }
        
        if (_baselineEventsPerSecond == 0) return 0;
        
        return ((currentEventsPerSecond - _baselineEventsPerSecond) / _baselineEventsPerSecond) * 100;
    }
    
    /// <summary>
    /// Buffer a vector for batch processing or process immediately if batching is disabled
    /// </summary>
    private async Task BufferVectorForBatch(LogEvent logEvent, float[] embedding, CancellationToken ct)
    {
        var options = _pipelineOptions.CurrentValue;
        
        if (!options.EnableVectorBatching)
        {
            // Batching disabled, process immediately
            await store.UpsertAsync(logEvent, embedding, ct);
            return;
        }
        
        bool shouldFlushBatch = false;
        
        lock (_vectorBufferLock)
        {
            _vectorBuffer.Add((logEvent, embedding));
            Interlocked.Increment(ref _totalVectorsBatched);
            
            // Check if we've reached the batch size limit
            shouldFlushBatch = _vectorBuffer.Count >= options.VectorBatchSize;
        }
        
        if (shouldFlushBatch)
        {
            await FlushVectorBatch(ct);
        }
    }
    
    /// <summary>
    /// Flush the current vector buffer to the vector store
    /// </summary>
    private async Task FlushVectorBatch(CancellationToken ct)
    {
        if (_isFlushingBatch) return; // Avoid concurrent flushes
        
        List<(LogEvent logEvent, float[] embedding)> itemsToFlush;
        
        lock (_vectorBufferLock)
        {
            if (_vectorBuffer.Count == 0) return; // Nothing to flush
            
            itemsToFlush = new List<(LogEvent, float[])>(_vectorBuffer);
            _vectorBuffer.Clear();
            _isFlushingBatch = true;
        }
        
        try
        {
            var batchStartTime = DateTimeOffset.UtcNow;
            
            await store.BatchUpsertAsync(itemsToFlush, ct);
            
            var batchTime = (DateTimeOffset.UtcNow - batchStartTime).TotalMilliseconds;
            Interlocked.Increment(ref _totalBatchFlushes);
            
            // Record batch metrics
            performanceMonitor.RecordVectorStoreMetrics(
                embeddingTimeMs: 0, // Embedding already done
                upsertTimeMs: batchTime, 
                searchTimeMs: 0, // No search in batch
                vectorsProcessed: itemsToFlush.Count
            );
            
            if (log.IsEnabled(LogLevel.Debug))
            {
                log.LogDebug("Flushed vector batch: {Count} vectors in {BatchTimeMs:F2}ms", 
                    itemsToFlush.Count, batchTime);
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error flushing vector batch of {Count} items", itemsToFlush.Count);
            
            // Record error metrics
            performanceMonitor.RecordVectorStoreMetrics(
                embeddingTimeMs: 0,
                upsertTimeMs: 0,
                searchTimeMs: 0,
                vectorsProcessed: 0,
                error: ex.Message
            );
            
            // Re-throw to handle upstream
            throw;
        }
        finally
        {
            _isFlushingBatch = false;
        }
    }
    
    /// <summary>
    /// Initialize batch flush timer for time-based flushing
    /// </summary>
    private void InitializeBatchFlushTimer()
    {
        var options = _pipelineOptions.CurrentValue;
        
        if (!options.EnableVectorBatching)
        {
            _batchFlushTimer?.Dispose();
            _batchFlushTimer = null;
            return;
        }
        
        // Create timer for periodic batch flushing
        _batchFlushTimer?.Dispose();
        _batchFlushTimer = new System.Threading.Timer(async _ =>
        {
            try
            {
                await FlushVectorBatch(CancellationToken.None);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Error during scheduled batch flush");
            }
        }, null, TimeSpan.FromMilliseconds(options.VectorBatchTimeoutMs), 
             TimeSpan.FromMilliseconds(options.VectorBatchTimeoutMs));
    }
    
    /// <summary>
    /// Get current batch processing metrics
    /// </summary>
    public (int CurrentBufferSize, long TotalVectorsBatched, long TotalBatchFlushes) GetBatchMetrics()
    {
        lock (_vectorBufferLock)
        {
            return (_vectorBuffer.Count, _totalVectorsBatched, _totalBatchFlushes);
        }
    }
    
    /// <summary>
    /// Dispose semaphore and batch timer resources
    /// </summary>
    public override void Dispose()
    {
        // Flush any remaining vectors before disposing
        try
        {
            if (_vectorBuffer.Count > 0)
            {
                FlushVectorBatch(CancellationToken.None).Wait(TimeSpan.FromSeconds(5));
            }
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Error flushing remaining vectors during disposal");
        }
        
        _batchFlushTimer?.Dispose();
        _batchFlushTimer = null;
        
        lock (_semaphoreLock)
        {
            _processingSemaphore?.Dispose();
            _processingSemaphore = null;
        }
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Initialize semaphore based on current configuration
        InitializeSemaphore();
        
        // Initialize batch flush timer
        InitializeBatchFlushTimer();
        
        // Listen for configuration changes and reinitialize semaphore and batch timer
        _pipelineOptions.OnChange((options, name) =>
        {
            log.LogInformation("Pipeline configuration changed, reinitializing semaphore and batch timer");
            InitializeSemaphore();
            InitializeBatchFlushTimer();
        });
        
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
            var semaphoreAcquired = false;
            
            try
            {
                eventCount++;
                queueDepth++; // Simplified queue depth tracking
                
                // Try to acquire semaphore permission for throttling (Sprint 2 Phase 3 - simplified logic)
                // Only set semaphoreAcquired if we actually have a semaphore to release later
                var hasSemaphore = _processingSemaphore != null;
                if (hasSemaphore)
                {
                    semaphoreAcquired = await TryAcquireSemaphoreAsync(ct);

                    if (!semaphoreAcquired)
                    {
                        var options = _pipelineOptions.CurrentValue;
                        if (options.SkipOnThrottleTimeout)
                        {
                            log.LogDebug("Skipping event {EventId} due to throttling timeout", e.EventId);
                        }
                        else
                        {
                            log.LogWarning("Dropping event {EventId} due to throttling timeout", e.EventId);
                        }
                        queueDepth--;
                        continue;
                    }
                }
                else
                {
                    // No semaphore enabled, so no need to release anything later
                    semaphoreAcquired = false;
                }
                
                // M4: Enhanced analysis with correlation and fusion
                var securityEvent = await AnalyzeEventWithCorrelation(e, ct);
                
                if (securityEvent != null)
                {
                    await ProcessSecurityEvent(securityEvent, ct);
                }
                
                queueDepth--;
                
                // Record pipeline performance metrics including throttling stats
                var processingTime = (DateTimeOffset.UtcNow - processingStartTime).TotalMilliseconds;
                var currentQueueDepth = GetCurrentSemaphoreQueueDepth();
                performanceMonitor.RecordPipelineMetrics(processingTime, 1, currentQueueDepth);
                
                // Record throttling metrics if semaphore was used
                if (_processingSemaphore != null)
                {
                    var (queueCount, acquisitions, timeouts, avgWaitTimeMs) = GetThrottlingMetrics();
                    var concurrentTasks = _pipelineOptions.CurrentValue.MaxConcurrentTasks - (_processingSemaphore?.CurrentCount ?? 0);
                    performanceMonitor.RecordPipelineThrottling((int)queueCount, avgWaitTimeMs, semaphoreAcquired, concurrentTasks);
                }
                
                // Record detailed pipeline metrics every 10 events
                if (eventCount % 10 == 0)
                {
                    var eventsPerSecond = CalculateCurrentEventsPerSecond(eventCount, _startTime);
                    var throughputImprovement = CalculateThroughputImprovement(eventsPerSecond);
                    performanceMonitor.RecordDetailedPipelineMetrics(eventsPerSecond, processingTime, throughputImprovement);
                }
            }
            catch (Exception ex)
            {
                queueDepth--;
                log.LogError(ex, "Pipeline error for event {EventId}", e.EventId);
                
                // Record pipeline error metrics
                var processingTime = (DateTimeOffset.UtcNow - processingStartTime).TotalMilliseconds;
                var currentQueueDepth = GetCurrentSemaphoreQueueDepth();
                performanceMonitor.RecordPipelineMetrics(processingTime, 0, currentQueueDepth, ex.Message);
            }
            finally
            {
                // Always release semaphore if it was acquired
                if (semaphoreAcquired)
                {
                    ReleaseSemaphore();
                }
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
        // STAGE 1: Execute independent operations in parallel
        var (enrichmentData, deterministicEvent, preparedText) = 
            await ExecuteIndependentOperationsParallel(logEvent, ct);
        
        SecurityEvent? llmEvent = null;
        
        // STAGE 2: Conditional LLM processing with parallel vector operations
        if (deterministicEvent == null || deterministicEvent.RiskLevel == "low")
        {
            try
            {
                // Generate embedding directly (caching removed for simplicity)
                var embeddingStartTime = DateTimeOffset.UtcNow;
                
                var embeddingResult = await GenerateEmbedding(preparedText, logEvent, ct);
                var embedding = embeddingResult.Embedding;
                var embeddingTime = embeddingResult.RetrievalTimeMs;
                
                log.LogDebug("Generated embedding for event {EventId} (generation: {GenTime:F1}ms)", 
                    logEvent.EventId, embeddingResult.GenerationTimeMs);
                
                log.LogDebug("Pipeline: embedding length={EmbeddingLength} for event {EventId}", embedding.Length, logEvent.EventId);
                
                // Parallel: Vector operations (upsert and search concurrently)
                var (upsertTime, searchTime, neighbors) = 
                    await ExecuteVectorOperationsParallel(logEvent, embedding, ct);
                
                // Record vector store metrics
                performanceMonitor.RecordVectorStoreMetrics(embeddingTime, upsertTime, searchTime, 1);
                
                // Sequential: LLM analysis (depends on search results)
                var llmStartTime = DateTimeOffset.UtcNow;
                var analysis = await llm.AnalyzeAsync(logEvent, neighbors.Select(n => n.evt), ct);
                var llmTime = (DateTimeOffset.UtcNow - llmStartTime).TotalMilliseconds;
                
                // Record LLM metrics
                performanceMonitor.RecordLlmMetrics("Ollama", "llama3.1", llmTime, 0, true);
                
                // Create security event from LLM response
                llmEvent = SecurityEvent.CreateFromLlmResponse(logEvent, analysis);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "LLM analysis failed for event {EventId}, continuing with deterministic analysis only", logEvent.EventId);
                
                // Record failed metrics
                performanceMonitor.RecordVectorStoreMetrics(error: ex.Message);
                performanceMonitor.RecordLlmMetrics("Ollama", "llama3.1", 0, 0, false);
            }
        }

        // STAGE 3: Rules engine correlation (sequential, depends on all results)
        log.LogInformation("Before rules engine: deterministicEvent={DeterministicEvent}, llmEvent={LlmEvent}", 
            deterministicEvent?.RiskLevel ?? "null", llmEvent?.RiskLevel ?? "null");
        
        var fusedEvent = rulesEngine.AnalyzeWithCorrelation(logEvent, deterministicEvent, llmEvent);
        
        // STAGE 4: Final enrichment (add IP enrichment data to the fused event)
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

        // Skip saving events that don't meet minimum correlation intelligence thresholds
        // Only applies to non-deterministic, non-enhanced events (pure LLM events)
        var options = _pipelineOptions.CurrentValue;
        if (!securityEvent.IsDeterministic &&
            !securityEvent.IsCorrelationBased &&
            !securityEvent.IsEnhanced)
        {
            // Check if all scores are below their respective thresholds
            bool belowCorrelationThreshold = securityEvent.CorrelationScore < options.MinCorrelationScoreThreshold;
            bool belowBurstThreshold = securityEvent.BurstScore < options.MinBurstScoreThreshold;
            bool belowAnomalyThreshold = securityEvent.AnomalyScore < options.MinAnomalyScoreThreshold;

            if (belowCorrelationThreshold && belowBurstThreshold && belowAnomalyThreshold)
            {
                log.LogDebug("Skipping storage of event {EventId} - scores below thresholds (corr={Corr:F2}<{MinCorr:F2}, burst={Burst:F2}<{MinBurst:F2}, anomaly={Anomaly:F2}<{MinAnomaly:F2})",
                    e.EventId,
                    securityEvent.CorrelationScore, options.MinCorrelationScoreThreshold,
                    securityEvent.BurstScore, options.MinBurstScoreThreshold,
                    securityEvent.AnomalyScore, options.MinAnomalyScoreThreshold);
                return; // Skip this event entirely
            }
        }

        // Skip events that match benign ignore patterns
        if (ignorePatternService.ShouldIgnoreEvent(securityEvent))
        {
            log.LogDebug("Skipping storage of event {EventId} - matches ignore pattern (EventType={EventType}, MITRE={Techniques})",
                e.EventId, securityEvent.EventType, string.Join(",", securityEvent.MitreTechniques));
            return; // Skip this event entirely
        }

        // Store the security event for API access (async for performance)
        await securityEventStore.AddSecurityEventAsync(securityEvent, ct);
        
        // YARA Integration: Scan files if auto-scanning is enabled
        await PerformYaraScanIfEnabled(securityEvent, ct);
        
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

    // PARALLEL PROCESSING HELPER METHODS

    /// <summary>
    /// Prepares text for embedding generation from a LogEvent
    /// </summary>
    private string PrepareTextForEmbedding(LogEvent logEvent)
    {
        return $"{logEvent.Channel} {logEvent.EventId} {logEvent.Level} {logEvent.User}\n{logEvent.Message}";
    }

    /// <summary>
    /// Executes independent operations in parallel: IP enrichment, deterministic detection, and text preparation
    /// </summary>
    private async Task<(string? enrichmentData, SecurityEvent? deterministicEvent, string preparedText)> 
        ExecuteIndependentOperationsParallel(LogEvent logEvent, CancellationToken ct)
    {
        if (!_pipelineOptions.CurrentValue.EnableParallelProcessing)
        {
            // Fall back to sequential processing
            var enrichmentData = await EnrichEventIPs(logEvent, ct);
            var deterministicEvent = securityDetector.DetectSecurityEvent(logEvent);
            var preparedText = PrepareTextForEmbedding(logEvent);
            return (enrichmentData, deterministicEvent, preparedText);
        }

        try
        {
            // Create timeout token for parallel operations
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(_pipelineOptions.CurrentValue.ParallelOperationTimeoutMs);

            // Execute independent operations in parallel
            var ipEnrichmentTask = EnrichEventIPs(logEvent, timeoutCts.Token);
            var deterministicDetectionTask = Task.Run(() => securityDetector.DetectSecurityEvent(logEvent), timeoutCts.Token);
            var textPreparationTask = Task.Run(() => PrepareTextForEmbedding(logEvent), timeoutCts.Token);

            // Wait for all parallel operations to complete
            await Task.WhenAll(ipEnrichmentTask, deterministicDetectionTask, textPreparationTask);

            return (
                await ipEnrichmentTask,
                await deterministicDetectionTask,
                await textPreparationTask
            );
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Main cancellation token was cancelled
            throw;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Parallel operations failed for event {EventId}, falling back to sequential processing", logEvent.EventId);
            
            // Fall back to sequential processing on error
            var enrichmentData = await EnrichEventIPs(logEvent, ct);
            var deterministicEvent = securityDetector.DetectSecurityEvent(logEvent);
            var preparedText = PrepareTextForEmbedding(logEvent);
            return (enrichmentData, deterministicEvent, preparedText);
        }
    }

    /// <summary>
    /// Generates an embedding for the given text with performance metrics
    /// </summary>
    private async Task<EmbeddingResult> GenerateEmbedding(string text, LogEvent logEvent, CancellationToken ct)
    {
        var startTime = DateTimeOffset.UtcNow;
        var embedding = await embedder.EmbedAsync(text, ct);
        var generationTime = (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;
        
        return new EmbeddingResult
        {
            Embedding = embedding,
            WasCached = false,
            RetrievalTimeMs = generationTime,
            GenerationTimeMs = generationTime
        };
    }

    /// <summary>
    /// Result of an embedding generation operation with performance metrics.
    /// </summary>
    private class EmbeddingResult
    {
        /// <summary>
        /// The embedding array.
        /// </summary>
        public float[] Embedding { get; set; } = Array.Empty<float>();

        /// <summary>
        /// Whether the embedding was retrieved from cache (always false after cache removal).
        /// </summary>
        public bool WasCached { get; set; }

        /// <summary>
        /// Total time for retrieval (generation) in milliseconds.
        /// </summary>
        public double RetrievalTimeMs { get; set; }

        /// <summary>
        /// Time taken for embedding generation in milliseconds.
        /// </summary>
        public double GenerationTimeMs { get; set; }

        /// <summary>
        /// Performance improvement ratio (always 1.0 after cache removal).
        /// </summary>
        public double SpeedupRatio => 1.0;
    }
    
    /// <summary>
    /// Executes vector operations (batch upsert and search) with optimal performance
    /// </summary>
    private async Task<(double upsertTime, double searchTime, IReadOnlyList<(LogEvent evt, float score)> neighbors)> 
        ExecuteVectorOperationsParallel(LogEvent logEvent, float[] embedding, CancellationToken ct)
    {
        if (!_pipelineOptions.CurrentValue.EnableParallelVectorOperations)
        {
            // Fall back to sequential vector operations
            var upsertStartTime = DateTimeOffset.UtcNow;
            await BufferVectorForBatch(logEvent, embedding, ct);
            var upsertTime = (DateTimeOffset.UtcNow - upsertStartTime).TotalMilliseconds;

            var searchStartTime = DateTimeOffset.UtcNow;
            var neighbors = await store.SearchAsync(embedding, k: 8, ct);
            var searchTime = (DateTimeOffset.UtcNow - searchStartTime).TotalMilliseconds;

            return (upsertTime, searchTime, neighbors);
        }

        try
        {
            // Execute vector operations in parallel: batch upsert + immediate search
            var upsertStartTime = DateTimeOffset.UtcNow;
            var searchStartTime = DateTimeOffset.UtcNow;

            // Use batch processing for upsert (better performance for high volume)
            var upsertTask = BufferVectorForBatch(logEvent, embedding, ct);
            // Keep search immediate (required for LLM analysis)
            var searchTask = store.SearchAsync(embedding, k: 8, ct);

            await Task.WhenAll(upsertTask, searchTask);

            var upsertTime = (DateTimeOffset.UtcNow - upsertStartTime).TotalMilliseconds;
            var searchTime = (DateTimeOffset.UtcNow - searchStartTime).TotalMilliseconds;
            var neighbors = await searchTask;

            return (upsertTime, searchTime, neighbors);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Parallel vector operations failed for event {EventId}, falling back to sequential", logEvent.EventId);
            
            // Fall back to sequential vector operations on error
            var upsertStartTime = DateTimeOffset.UtcNow;
            await BufferVectorForBatch(logEvent, embedding, ct);
            var upsertTime = (DateTimeOffset.UtcNow - upsertStartTime).TotalMilliseconds;

            var searchStartTime = DateTimeOffset.UtcNow;
            var neighbors = await store.SearchAsync(embedding, k: 8, ct);
            var searchTime = (DateTimeOffset.UtcNow - searchStartTime).TotalMilliseconds;

            return (upsertTime, searchTime, neighbors);
        }
    }
    
    /// <summary>
    /// Performs YARA scanning on files associated with a security event if auto-scanning is enabled
    /// </summary>
    private async Task PerformYaraScanIfEnabled(SecurityEvent securityEvent, CancellationToken ct)
    {
        try
        {
            var yaraOptions = _yaraScanningOptions.CurrentValue;
            
            // Check if YARA scanning is enabled and meets threshold
            if (!yaraOptions.Enabled || !yaraOptions.AutoScanSecurityEvents)
            {
                return;
            }
            
            // Check if security event meets minimum threat level for scanning
            if (!ShouldScanBasedOnThreatLevel(securityEvent.RiskLevel, yaraOptions.MinThreatLevel))
            {
                log.LogDebug("🔍 Skipping YARA scan for event {EventId}: threat level {RiskLevel} below minimum {MinThreatLevel}", 
                    securityEvent.OriginalEvent.EventId, securityEvent.RiskLevel, yaraOptions.MinThreatLevel);
                return;
            }
            
            // Extract file paths from the security event
            var filePaths = ExtractFilePathsFromSecurityEvent(securityEvent);
            if (!filePaths.Any())
            {
                log.LogDebug("🔍 No file paths found in security event {EventId} for YARA scanning", 
                    securityEvent.OriginalEvent.EventId);
                return;
            }
            
            log.LogInformation("🔍 YARA scanning triggered for security event {EventId} with {FileCount} files", 
                securityEvent.OriginalEvent.EventId, filePaths.Count);
            
            // Perform YARA scanning on each file
            foreach (var filePath in filePaths)
            {
                try
                {
                    if (!File.Exists(filePath))
                    {
                        log.LogDebug("🔍 Skipping YARA scan: file not found {FilePath}", filePath);
                        continue;
                    }
                    
                    log.LogDebug("🔍 YARA scanning file: {FilePath}", filePath);
                    var matches = await _malwareScanService.ScanFileAsync(filePath, ct);
                    
                    if (matches.Any())
                    {
                        log.LogWarning("🔍 YARA matches found in {FilePath}: {MatchCount} rules matched", 
                            filePath, matches.Count());
                            
                        // Link YARA matches to the security event
                        await LinkMalwareMatchesToSecurityEvent(matches, securityEvent, ct);
                    }
                    else
                    {
                        log.LogDebug("🔍 YARA scan completed for {FilePath}: no matches", filePath);
                    }
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "🔍 YARA scan failed for file {FilePath}", filePath);
                }
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "🔍 Error during YARA scanning integration for event {EventId}", 
                securityEvent.OriginalEvent.EventId);
        }
    }
    
    /// <summary>
    /// Determines if YARA scanning should be performed based on threat level
    /// </summary>
    private static bool ShouldScanBasedOnThreatLevel(string eventRiskLevel, string minThreatLevel)
    {
        var riskLevels = new Dictionary<string, int>
        {
            { "low", 1 },
            { "medium", 2 },
            { "high", 3 },
            { "critical", 4 }
        };
        
        if (!riskLevels.TryGetValue(eventRiskLevel.ToLowerInvariant(), out var eventLevel))
        {
            return false; // Unknown risk level
        }
        
        if (!riskLevels.TryGetValue(minThreatLevel.ToLowerInvariant(), out var minLevel))
        {
            return false; // Unknown minimum threat level
        }
        
        return eventLevel >= minLevel;
    }
    
    /// <summary>
    /// Extracts file paths from a security event for YARA scanning
    /// </summary>
    private List<string> ExtractFilePathsFromSecurityEvent(SecurityEvent securityEvent)
    {
        var filePaths = new List<string>();
        var logEvent = securityEvent.OriginalEvent;
        
        try
        {
            // Process Creation events (EventId 4688) often contain file paths
            if (logEvent.Channel.Equals("Security", StringComparison.OrdinalIgnoreCase) && 
                logEvent.EventId == 4688 && 
                securityEvent.EventType == SecurityEventType.ProcessCreation)
            {
                // Extract process path from the message
                var processPath = ExtractProcessPathFromMessage(logEvent.Message);
                if (!string.IsNullOrEmpty(processPath))
                {
                    filePaths.Add(processPath);
                }
            }
            
            // PowerShell script block events might contain file references
            else if (logEvent.Channel.Contains("PowerShell", StringComparison.OrdinalIgnoreCase) && 
                     logEvent.EventId == 4104)
            {
                var scriptPaths = ExtractScriptPathsFromPowerShellEvent(logEvent.Message);
                filePaths.AddRange(scriptPaths);
            }
            
            // Service installation events might contain executable paths
            else if (logEvent.EventId == 7045 || logEvent.EventId == 4697)
            {
                var servicePath = ExtractServicePathFromMessage(logEvent.Message);
                if (!string.IsNullOrEmpty(servicePath))
                {
                    filePaths.Add(servicePath);
                }
            }
        }
        catch (Exception ex)
        {
            log.LogDebug(ex, "Error extracting file paths from security event {EventId}", logEvent.EventId);
        }
        
        return filePaths.Distinct().Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
    }
    
    /// <summary>
    /// Extracts process path from Windows Security event message
    /// </summary>
    private string? ExtractProcessPathFromMessage(string message)
    {
        try
        {
            // Look for common patterns in process creation events
            var patterns = new[]
            {
                @"New Process Name:\s*([C-Z]:\\[^\r\n]+)",
                @"Process Name:\s*([C-Z]:\\[^\r\n]+)",
                @"Application\s*([C-Z]:\\[^\r\n]+)",
                @"\""([C-Z]:\\[^\""]+)\.exe\"""
            };
            
            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(message, pattern, 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | 
                    System.Text.RegularExpressions.RegexOptions.Multiline);
                if (match.Success && match.Groups.Count > 1)
                {
                    var path = match.Groups[1].Value.Trim();
                    if (Path.IsPathRooted(path) && path.Length > 3)
                    {
                        return path;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log.LogDebug(ex, "Error extracting process path from message");
        }
        
        return null;
    }
    
    /// <summary>
    /// Extracts script file paths from PowerShell events
    /// </summary>
    private List<string> ExtractScriptPathsFromPowerShellEvent(string message)
    {
        var paths = new List<string>();
        
        try
        {
            // Look for file paths in PowerShell script blocks
            var patterns = new[]
            {
                @"\. ([C-Z]:\\[^\s\r\n]+\.ps1)",
                @"& \""([C-Z]:\\[^\""]+\.ps1)\""",
                @"Invoke-Expression.*([C-Z]:\\[^\s\r\n]+\.ps1)",
                @"Import-Module ([C-Z]:\\[^\s\r\n]+\.(ps1|psm1))"
            };
            
            foreach (var pattern in patterns)
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(message, pattern, 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | 
                    System.Text.RegularExpressions.RegexOptions.Multiline);
                    
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        var path = match.Groups[1].Value.Trim();
                        if (Path.IsPathRooted(path))
                        {
                            paths.Add(path);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log.LogDebug(ex, "Error extracting script paths from PowerShell event");
        }
        
        return paths;
    }
    
    /// <summary>
    /// Extracts service executable path from service installation events
    /// </summary>
    private string? ExtractServicePathFromMessage(string message)
    {
        try
        {
            // Look for service executable paths
            var patterns = new[]
            {
                @"Service File Name:\s*([C-Z]:\\[^\r\n]+)",
                @"Binary Path Name:\s*([C-Z]:\\[^\r\n]+)",
                @"ImagePath:\s*([C-Z]:\\[^\r\n]+)"
            };
            
            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(message, pattern, 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | 
                    System.Text.RegularExpressions.RegexOptions.Multiline);
                if (match.Success && match.Groups.Count > 1)
                {
                    var path = match.Groups[1].Value.Trim();
                    if (Path.IsPathRooted(path) && path.Length > 3)
                    {
                        return path;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log.LogDebug(ex, "Error extracting service path from message");
        }
        
        return null;
    }
    
    /// <summary>
    /// Links YARA matches to a security event by setting the SecurityEventId
    /// </summary>
    private async Task LinkMalwareMatchesToSecurityEvent(IEnumerable<MalwareMatch> matches, SecurityEvent securityEvent, CancellationToken ct)
    {
        try
        {
            foreach (var match in matches)
            {
                // Set the security event ID to link the match to this event
                match.SecurityEventId = securityEvent.Id;
                
                // Save the updated match
                await _malwareRuleStore.SaveMatchAsync(match);
                
                log.LogInformation("🔍 YARA match linked to security event: Rule {RuleName} → Event {EventId}", 
                    match.RuleName, securityEvent.OriginalEvent.EventId);
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error linking YARA matches to security event {EventId}", 
                securityEvent.OriginalEvent.EventId);
        }
    }
}

