using Castellan.Core.Interfaces.Connection;
using Castellan.Core.Models.Connection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Castellan.Core.Services.Connection;

/// <summary>
/// Pooled HTTP Client implementation with retry logic and performance tracking
/// </summary>
public class PooledHttpClient : IPooledHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly string _poolName;
    private readonly string _clientId;
    private readonly HttpClientPoolConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly ConnectionPerformanceMetrics _metrics;
    private readonly object _metricsLock = new();

    private bool _isHealthy = true;
    private string? _unhealthyReason;
    private bool _disposed;

    public HttpClient HttpClient => _httpClient;
    public string PoolName => _poolName;
    public string ClientId => _clientId;
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset LastUsedAt { get; private set; }
    public long RequestCount { get; private set; }
    public bool IsHealthy => _isHealthy && !_disposed;

    public PooledHttpClient(
        HttpClient httpClient,
        string poolName,
        string clientId,
        HttpClientPoolConfiguration configuration,
        ILogger logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _poolName = poolName ?? throw new ArgumentNullException(nameof(poolName));
        _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        CreatedAt = DateTimeOffset.UtcNow;
        LastUsedAt = CreatedAt;

        _metrics = new ConnectionPerformanceMetrics
        {
            ConnectionId = clientId,
            PoolName = poolName,
            CreatedAt = CreatedAt,
            State = ConnectionState.Available
        };
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        return await SendAsync(request, new PooledRequestOptions(), cancellationToken);
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, PooledRequestOptions options, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PooledHttpClient));
        if (!_isHealthy) throw new InvalidOperationException($"HTTP client {_clientId} is marked as unhealthy: {_unhealthyReason}");

        ArgumentNullException.ThrowIfNull(request);
        options ??= new PooledRequestOptions();

        var stopwatch = Stopwatch.StartNew();
        var maxRetries = options.MaxRetries ?? _configuration.MaxRetries;
        var timeout = options.Timeout ?? _configuration.RequestTimeout;
        
        UpdateMetricsState(ConnectionState.InUse);

        try
        {
            // Apply request-specific options
            ApplyRequestOptions(request, options);

            // Create timeout token
            using var timeoutCts = new CancellationTokenSource(timeout);
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            HttpResponseMessage? response = null;
            Exception? lastException = null;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // Clone request for retry attempts (except first)
                    var requestToSend = attempt == 0 ? request : await CloneRequestAsync(request);

                    _logger.LogDebug("Sending HTTP request attempt {Attempt}/{MaxAttempts} for client {ClientId}", 
                        attempt + 1, maxRetries + 1, _clientId);

                    response = await _httpClient.SendAsync(requestToSend, combinedCts.Token);

                    // Check if response indicates success or if we should retry
                    if (IsSuccessStatusCode(response.StatusCode) || !ShouldRetry(response.StatusCode, options.Priority))
                    {
                        break;
                    }

                    // Store the response as it might be the final attempt
                    if (attempt == maxRetries)
                    {
                        break;
                    }

                    // Clean up response for retry
                    response?.Dispose();
                    response = null;

                    // Wait before retry with exponential backoff
                    var delay = CalculateRetryDelay(attempt, options.Priority);
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, combinedCts.Token);
                    }
                }
                catch (Exception ex) when (attempt < maxRetries && IsRetryableException(ex))
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "HTTP request attempt {Attempt} failed for client {ClientId}, retrying...", 
                        attempt + 1, _clientId);

                    // Wait before retry
                    var delay = CalculateRetryDelay(attempt, options.Priority);
                    if (delay > TimeSpan.Zero && !combinedCts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(delay, combinedCts.Token);
                    }
                }
            }

            stopwatch.Stop();

            if (response == null)
            {
                // All retries failed
                var finalException = lastException ?? new HttpRequestException("Request failed after all retry attempts");
                RecordRequestFailure(stopwatch.Elapsed);
                throw finalException;
            }

            // Record success/failure metrics
            if (IsSuccessStatusCode(response.StatusCode))
            {
                RecordRequestSuccess(stopwatch.Elapsed);
            }
            else
            {
                RecordRequestFailure(stopwatch.Elapsed);
            }

            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            RecordRequestFailure(stopwatch.Elapsed);
            throw;
        }
        catch (OperationCanceledException) // Timeout
        {
            stopwatch.Stop();
            RecordRequestFailure(stopwatch.Elapsed);
            throw new TimeoutException($"HTTP request timed out after {timeout.TotalSeconds} seconds");
        }
        catch (Exception)
        {
            stopwatch.Stop();
            RecordRequestFailure(stopwatch.Elapsed);
            throw;
        }
        finally
        {
            UpdateMetricsState(ConnectionState.Available);
            LastUsedAt = DateTimeOffset.UtcNow;
        }
    }

    public ConnectionPerformanceMetrics GetMetrics()
    {
        lock (_metricsLock)
        {
            _metrics.LastUsedAt = LastUsedAt;
            _metrics.RequestCount = RequestCount;
            return _metrics;
        }
    }

    public async Task<ConnectionHealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var result = new ConnectionHealthCheckResult
        {
            ConnectionId = _clientId,
            CheckType = "client",
            IsHealthy = true
        };

        var startTime = DateTimeOffset.UtcNow;

        try
        {
            // Basic health checks
            result.Details["is_disposed"] = _disposed;
            result.Details["is_healthy"] = _isHealthy;
            result.Details["request_count"] = RequestCount;
            result.Details["error_rate"] = _metrics.ErrorRate;
            result.Details["created_at"] = CreatedAt;
            result.Details["last_used_at"] = LastUsedAt;

            if (_disposed)
            {
                result.IsHealthy = false;
                result.ErrorMessage = "Client is disposed";
            }
            else if (!_isHealthy)
            {
                result.IsHealthy = false;
                result.ErrorMessage = _unhealthyReason ?? "Client marked as unhealthy";
            }
            else if (_metrics.ErrorRate > 50) // 50% error rate threshold
            {
                result.IsHealthy = false;
                result.ErrorMessage = "High error rate detected";
            }

            result.ResponseTime = DateTimeOffset.UtcNow - startTime;
        }
        catch (Exception ex)
        {
            result.IsHealthy = false;
            result.ErrorMessage = ex.Message;
            result.ResponseTime = DateTimeOffset.UtcNow - startTime;
        }

        return result;
    }

    public void ResetStatistics()
    {
        lock (_metricsLock)
        {
            RequestCount = 0;
            _metrics.RequestCount = 0;
            _metrics.ErrorCount = 0;
            _metrics.AverageResponseTime = TimeSpan.Zero;
            _metrics.BytesSent = 0;
            _metrics.BytesReceived = 0;
        }

        _logger.LogDebug("Reset statistics for HTTP client {ClientId}", _clientId);
    }

    public void MarkUnhealthy(string reason)
    {
        _isHealthy = false;
        _unhealthyReason = reason;
        _logger.LogWarning("Marked HTTP client {ClientId} as unhealthy: {Reason}", _clientId, reason);
    }

    public void MarkHealthy()
    {
        _isHealthy = true;
        _unhealthyReason = null;
        _logger.LogDebug("Marked HTTP client {ClientId} as healthy", _clientId);
    }

    private void ApplyRequestOptions(HttpRequestMessage request, PooledRequestOptions options)
    {
        // Apply custom headers
        if (options.CustomHeaders != null)
        {
            foreach (var header in options.CustomHeaders)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        // Apply compression (this would typically be set on the HttpClient, but we can hint via headers)
        if (options.EnableCompression.HasValue && options.EnableCompression.Value)
        {
            request.Headers.AcceptEncoding.Clear();
            request.Headers.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
            request.Headers.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("deflate"));
        }
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage originalRequest)
    {
        var clonedRequest = new HttpRequestMessage(originalRequest.Method, originalRequest.RequestUri)
        {
            Version = originalRequest.Version
        };

        // Copy headers
        foreach (var header in originalRequest.Headers)
        {
            clonedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Copy content if present
        if (originalRequest.Content != null)
        {
            var contentBytes = await originalRequest.Content.ReadAsByteArrayAsync();
            clonedRequest.Content = new ByteArrayContent(contentBytes);

            // Copy content headers
            foreach (var header in originalRequest.Content.Headers)
            {
                clonedRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        // Copy properties
        foreach (var property in originalRequest.Options)
        {
            clonedRequest.Options.Set(new HttpRequestOptionsKey<object?>(property.Key), property.Value);
        }

        return clonedRequest;
    }

    private static bool IsSuccessStatusCode(HttpStatusCode statusCode)
    {
        return (int)statusCode >= 200 && (int)statusCode <= 299;
    }

    private static bool ShouldRetry(HttpStatusCode statusCode, RequestPriority priority)
    {
        // Don't retry client errors (4xx) except for specific cases
        if ((int)statusCode >= 400 && (int)statusCode < 500)
        {
            return statusCode switch
            {
                HttpStatusCode.RequestTimeout => true,
                HttpStatusCode.TooManyRequests => true,
                _ => false
            };
        }

        // Retry server errors (5xx) and network errors
        if ((int)statusCode >= 500)
        {
            return true;
        }

        return false;
    }

    private static bool IsRetryableException(Exception exception)
    {
        return exception switch
        {
            HttpRequestException => true,
            TaskCanceledException when !exception.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) => false,
            TaskCanceledException => true, // Timeout
            SocketException => true,
            _ => false
        };
    }

    private TimeSpan CalculateRetryDelay(int attemptNumber, RequestPriority priority)
    {
        // Base delay in milliseconds
        var baseDelay = priority switch
        {
            RequestPriority.Critical => 100,
            RequestPriority.High => 200,
            RequestPriority.Normal => 500,
            RequestPriority.Low => 1000,
            _ => 500
        };

        // Exponential backoff with jitter
        var exponentialDelay = baseDelay * Math.Pow(2, attemptNumber);
        var jitter = Random.Shared.NextDouble() * 0.1; // 10% jitter
        var finalDelay = exponentialDelay * (1 + jitter);

        // Cap the delay
        var maxDelay = priority switch
        {
            RequestPriority.Critical => 1000,  // 1 second max
            RequestPriority.High => 2000,      // 2 seconds max
            RequestPriority.Normal => 5000,    // 5 seconds max
            RequestPriority.Low => 10000,      // 10 seconds max
            _ => 5000
        };

        return TimeSpan.FromMilliseconds(Math.Min(finalDelay, maxDelay));
    }

    private void RecordRequestSuccess(TimeSpan responseTime)
    {
        lock (_metricsLock)
        {
            RequestCount++;
            _metrics.RequestCount = RequestCount;
            
            // Update average response time (simple moving average)
            var totalTime = (_metrics.AverageResponseTime.TotalMilliseconds * (RequestCount - 1)) + responseTime.TotalMilliseconds;
            _metrics.AverageResponseTime = TimeSpan.FromMilliseconds(totalTime / RequestCount);

            _logger.LogDebug("HTTP request succeeded for client {ClientId} in {ResponseTime}ms", 
                _clientId, responseTime.TotalMilliseconds);
        }
    }

    private void RecordRequestFailure(TimeSpan responseTime)
    {
        lock (_metricsLock)
        {
            RequestCount++;
            _metrics.RequestCount = RequestCount;
            _metrics.ErrorCount++;

            // Update average response time (including failed requests)
            var totalTime = (_metrics.AverageResponseTime.TotalMilliseconds * (RequestCount - 1)) + responseTime.TotalMilliseconds;
            _metrics.AverageResponseTime = TimeSpan.FromMilliseconds(totalTime / RequestCount);

            _logger.LogWarning("HTTP request failed for client {ClientId} after {ResponseTime}ms (Error rate: {ErrorRate:F1}%)", 
                _clientId, responseTime.TotalMilliseconds, _metrics.ErrorRate);
        }
    }

    private void UpdateMetricsState(ConnectionState state)
    {
        lock (_metricsLock)
        {
            _metrics.State = state;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _httpClient?.Dispose();

        _logger.LogDebug("Disposed HTTP client {ClientId} from pool {PoolName}", _clientId, _poolName);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _httpClient?.Dispose();

        _logger.LogDebug("Disposed HTTP client {ClientId} from pool {PoolName}", _clientId, _poolName);
        GC.SuppressFinalize(this);
    }
}
