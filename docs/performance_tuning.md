# Castellan Performance Tuning Guide

**Document Version**: 1.0  
**Created**: January 2025  
**Phase**: 3 Performance Optimization Implementation  

## Overview

This document provides comprehensive guidance for tuning Castellan's performance, particularly the Phase 3 optimizations that introduce semaphore-based throttling, memory management, and enhanced monitoring capabilities.

## Performance Architecture

### Phase 3 Enhancements

The Phase 3 performance optimization introduces several key improvements:

- **Semaphore-Based Throttling**: Configurable concurrency limits with graceful degradation
- **Memory Management**: Retention policies and automatic cleanup
- **Enhanced Monitoring**: Detailed metrics with throughput tracking
- **Queue Management**: Back-pressure handling and overflow protection

## Configuration Reference

### Pipeline Performance Settings

#### Core Processing
```json
{
  "Pipeline": {
    "EnableParallelProcessing": true,
    "MaxConcurrency": 4,
    "ParallelOperationTimeoutMs": 30000,
    "EnableParallelVectorOperations": true,
    "BatchSize": 100,
    "ProcessingIntervalMs": 1000
  }
}
```

#### Throttling & Concurrency Control
```json
{
  "Pipeline": {
    "EnableSemaphoreThrottling": true,
    "MaxConcurrentTasks": 8,
    "SemaphoreTimeoutMs": 15000,
    "SkipOnThrottleTimeout": false,
    "EnableAdaptiveThrottling": false,
    "CpuThrottleThreshold": 80
  }
}
```

#### Memory Management & Retention
```json
{
  "Pipeline": {
    "EventHistoryRetentionMinutes": 60,
    "MaxEventsPerCorrelationKey": 1000,
    "MemoryHighWaterMarkMB": 1024,
    "MemoryCleanupIntervalMinutes": 10,
    "EnableAggressiveGarbageCollection": false,
    "EnableMemoryPressureMonitoring": true
  }
}
```

#### Queue Management
```json
{
  "Pipeline": {
    "MaxQueueDepth": 1000,
    "EnableQueueBackPressure": true,
    "DropOldestOnQueueFull": false
  }
}
```

#### Performance Monitoring
```json
{
  "Pipeline": {
    "EnableDetailedMetrics": true,
    "MetricsIntervalMs": 30000,
    "EnablePerformanceAlerts": true
  }
}
```

## Performance Tuning Strategies

### 1. Optimize for High Throughput

For environments with high event volume:

```json
{
  "Pipeline": {
    "EnableSemaphoreThrottling": true,
    "MaxConcurrentTasks": 16,
    "SemaphoreTimeoutMs": 30000,
    "SkipOnThrottleTimeout": false,
    "MaxQueueDepth": 5000,
    "EnableQueueBackPressure": true,
    "BatchSize": 200
  }
}
```

**Recommendations**:
- Increase `MaxConcurrentTasks` to 2x CPU cores for CPU-bound workloads
- Set higher `SemaphoreTimeoutMs` for network-heavy operations
- Enable queue back-pressure to prevent memory exhaustion

### 2. Optimize for Low Latency

For environments requiring fast response times:

```json
{
  "Pipeline": {
    "EnableSemaphoreThrottling": true,
    "MaxConcurrentTasks": 4,
    "SemaphoreTimeoutMs": 5000,
    "SkipOnThrottleTimeout": true,
    "MaxQueueDepth": 100,
    "DropOldestOnQueueFull": true,
    "BatchSize": 50
  }
}
```

**Recommendations**:
- Lower `SemaphoreTimeoutMs` for faster timeout detection
- Enable `SkipOnThrottleTimeout` to avoid blocking
- Use smaller batch sizes for reduced processing latency

### 3. Optimize for Memory-Constrained Environments

For systems with limited memory:

```json
{
  "Pipeline": {
    "EventHistoryRetentionMinutes": 30,
    "MaxEventsPerCorrelationKey": 500,
    "MemoryHighWaterMarkMB": 512,
    "MemoryCleanupIntervalMinutes": 5,
    "EnableAggressiveGarbageCollection": true,
    "EnableMemoryPressureMonitoring": true
  }
}
```

**Recommendations**:
- Reduce retention times and event limits
- Lower memory thresholds and increase cleanup frequency
- Enable aggressive GC for memory-constrained environments

### 4. Optimize for CPU-Intensive Workloads

For environments with heavy CPU usage:

```json
{
  "Pipeline": {
    "EnableAdaptiveThrottling": true,
    "CpuThrottleThreshold": 70,
    "MaxConcurrentTasks": 6,
    "ParallelOperationTimeoutMs": 60000
  }
}
```

**Recommendations**:
- Enable adaptive throttling to respond to CPU pressure
- Set lower CPU thresholds for earlier throttling
- Increase operation timeouts for CPU-bound tasks

## Monitoring and Metrics

### Key Performance Indicators (KPIs)

#### Primary Metrics
- **Events Per Second**: Target >50% improvement over baseline
- **Processing Latency**: Average time per event (aim for <200ms)
- **Memory Stability**: Working set should remain stable under load
- **Semaphore Efficiency**: High acquisition success rate (>95%)

#### Secondary Metrics
- **Queue Depth**: Should remain below configured limits
- **GC Frequency**: Monitor for excessive garbage collection
- **Throughput Improvement**: Percentage gain vs baseline
- **Error Rates**: Processing errors per thousand events

### Performance Monitoring Tools

#### Built-in Metrics
Castellan provides comprehensive built-in metrics:

```csharp
// Pipeline throttling metrics
performanceMonitor.RecordPipelineThrottling(
    semaphoreQueueLength, 
    waitTimeMs, 
    acquisitionSuccessful, 
    concurrentTasks
);

// Detailed pipeline metrics
performanceMonitor.RecordDetailedPipelineMetrics(
    eventsPerSecond, 
    avgLatencyMs, 
    throughputImprovement
);

// Memory pressure monitoring
performanceMonitor.RecordMemoryPressure(
    memoryUsageMB, 
    gcPressure, 
    cleanupTriggered
);
```

#### Log Analysis
Monitor structured logs for performance indicators:

```
[Pipeline throttling]: queue=5, wait=150ms, success=true, running=8
[Memory pressure]: 1024MB, GC pressure: 2048KB, cleanup=false
[Pipeline detailed]: 45.2 events/sec, 180ms avg latency, 23% improvement
```

## Troubleshooting Performance Issues

### Common Performance Problems

#### 1. High Memory Usage
**Symptoms**: Increasing memory usage, frequent GC
**Solutions**:
- Reduce `EventHistoryRetentionMinutes`
- Lower `MaxEventsPerCorrelationKey`
- Enable `EnableAggressiveGarbageCollection`
- Decrease `MemoryCleanupIntervalMinutes`

#### 2. Low Throughput
**Symptoms**: Events per second below expectations
**Solutions**:
- Increase `MaxConcurrentTasks`
- Enable `EnableParallelVectorOperations`
- Increase `BatchSize` for bulk operations
- Check for semaphore timeout issues

#### 3. High Latency
**Symptoms**: Slow event processing times
**Solutions**:
- Reduce `SemaphoreTimeoutMs`
- Enable `SkipOnThrottleTimeout`
- Decrease `BatchSize`
- Check for queue back-pressure

#### 4. Frequent Throttling
**Symptoms**: Many semaphore timeouts in logs
**Solutions**:
- Increase `MaxConcurrentTasks`
- Increase `SemaphoreTimeoutMs`
- Enable `EnableAdaptiveThrottling`
- Check system resource usage

### Diagnostic Commands

#### Memory Diagnostics
```powershell
# Check memory usage
Get-Process "Castellan.Worker" | Select-Object WorkingSet64, VirtualMemorySize64

# Monitor GC collections
# Check logs for GC pressure indicators
```

#### Performance Diagnostics  
```powershell
# Monitor CPU usage
Get-Counter "\Processor(_Total)\% Processor Time"

# Check thread pool usage
# Look for thread pool starvation indicators in logs
```

## Best Practices

### Configuration Best Practices

1. **Start with defaults**: Use recommended default values initially
2. **Monitor before tuning**: Establish baseline metrics before optimization
3. **Change one setting at a time**: Isolate the impact of each change
4. **Test under load**: Validate changes under realistic load conditions
5. **Document changes**: Keep track of configuration modifications

### Operational Best Practices

1. **Regular monitoring**: Check performance metrics daily
2. **Proactive alerting**: Set up alerts for performance thresholds
3. **Capacity planning**: Monitor trends for resource planning
4. **Load testing**: Regular performance testing under peak loads
5. **Configuration validation**: Validate settings at startup

### Development Best Practices

1. **Profile before optimizing**: Use profiling tools to identify bottlenecks
2. **Measure impact**: Quantify performance improvements
3. **Consider trade-offs**: Balance throughput vs latency vs memory
4. **Test error scenarios**: Validate graceful degradation under failure
5. **Review regularly**: Periodic review of performance characteristics

## Advanced Tuning

### Environment-Specific Optimizations

#### Development Environment
- Disable detailed metrics for faster development cycles
- Use smaller retention times and limits
- Enable aggressive cleanup for memory recycling

#### Production Environment
- Enable comprehensive monitoring and alerting
- Use conservative concurrency limits initially
- Implement gradual load testing for validation

#### High-Performance Environment
- Maximize concurrency within system limits
- Optimize batch sizes for bulk operations
- Fine-tune memory thresholds for peak efficiency

### Integration Considerations

#### Database Performance
- Configure connection pooling appropriately
- Monitor database response times
- Consider database-specific optimizations

#### Vector Store Performance
- Optimize Qdrant connection settings
- Consider batch operations for bulk inserts
- Monitor vector operation latencies

#### Network Performance
- Account for network latency in timeouts
- Consider connection pooling and reuse
- Monitor external service response times

## Performance Validation

### Testing Strategy

1. **Baseline Measurement**: Establish current performance metrics
2. **Incremental Testing**: Test each optimization individually  
3. **Load Testing**: Validate under expected production loads
4. **Stress Testing**: Test behavior under extreme conditions
5. **Regression Testing**: Ensure no performance regressions

### Success Criteria

- **Throughput**: ≥50% improvement in events per second
- **Memory**: Stable memory usage under sustained load
- **Latency**: 95th percentile response time <200ms
- **Reliability**: >99.5% uptime with <0.1% error rate

---

**Note**: This document should be updated as performance characteristics change and new optimization techniques are implemented. Regular review ensures continued relevance and effectiveness.
