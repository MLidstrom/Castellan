using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;

namespace Castellan.Worker.VectorStores;

public sealed class QdrantVectorStore : IVectorStore
{
    private readonly HttpClient _http;
    private readonly QdrantOptions _opt;
    private readonly ILogger<QdrantVectorStore> _logger;
    private string C => _opt.Collection;

    public QdrantVectorStore(IOptions<QdrantOptions> opt, IHttpClientFactory httpFactory, ILogger<QdrantVectorStore> logger)
    {
        _opt = opt.Value;
        _http = httpFactory.CreateClient(nameof(QdrantVectorStore));
        _logger = logger;
        var scheme = _opt.Https ? "https" : "http";
        _http.BaseAddress = new Uri($"{scheme}://{_opt.Host}:{_opt.Port}/");
        if (!string.IsNullOrWhiteSpace(_opt.ApiKey))
        {
            _http.DefaultRequestHeaders.Add("api-key", _opt.ApiKey);
        }
    }

    public async Task EnsureCollectionAsync(CancellationToken ct)
    {
        var body = new CreateCollectionRequest
        {
            Vectors = new Dictionary<string, VectorParams>
            {
                ["log_events"] = new VectorParams { Size = _opt.VectorSize, Distance = NormalizeDistance(_opt.Distance) }
            }
        };
        using var resp = await _http.PutAsJsonAsync($"collections/{C}", body, ct);
        if (!resp.IsSuccessStatusCode)
        {
            // If already exists, Qdrant may return 409; ignore it
            if ((int)resp.StatusCode != 409)
            {
                var msg = await resp.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException($"Qdrant create collection failed: {(int)resp.StatusCode} {resp.ReasonPhrase} - {msg}");
            }
        }
    }

    public async Task UpsertAsync(LogEvent e, float[] embedding, CancellationToken ct)
    {
        // Generate a UUID for the point ID since Qdrant requires either unsigned integers or UUIDs
        // We'll use the UniqueId as a hash to generate a consistent UUID for the same event
        var pointId = GenerateConsistentUuid(e.UniqueId);
        
        var req = new UpsertPointsRequest
        {
            Points =
            [
                new UpsertPoint
                {
                    Id = pointId,
                    Vectors = new Dictionary<string, float[]>
                    {
                        ["log_events"] = embedding
                    },
                    Payload = new Dictionary<string, object?>
                    {
                        ["time"] = e.Time.ToString("o"),
                        ["host"] = e.Host,
                        ["channel"] = e.Channel,
                        ["eventId"] = e.EventId,
                        ["level"] = e.Level,
                        ["user"] = e.User,
                        ["message"] = e.Message,
                        ["uniqueId"] = e.UniqueId
                    }
                }
            ]
        };

        // Debug: Log basic request info without sensitive data
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Sending upsert request to Qdrant for point ID: {PointId}", pointId);
        }

        using var resp = await _http.PutAsJsonAsync($"collections/{C}/points", req, ct);
        
        if (!resp.IsSuccessStatusCode)
        {
            var errorContent = await resp.Content.ReadAsStringAsync(ct);
            _logger.LogError("Qdrant upsert failed: {StatusCode} {ReasonPhrase} - {ErrorContent}", 
                (int)resp.StatusCode, resp.ReasonPhrase, errorContent);
            throw new HttpRequestException($"Qdrant upsert failed: {(int)resp.StatusCode} {resp.ReasonPhrase} - {errorContent}");
        }
        
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Qdrant upsert successful for point ID: {PointId}", pointId);
        }
    }

    public async Task<bool> Has24HoursOfDataAsync(CancellationToken ct)
    {
        try
        {
            // Get collection info to check if we have any data
            var resp = await _http.GetAsync($"collections/{C}", ct);
            if (!resp.IsSuccessStatusCode)
            {
                // Collection doesn't exist, so we definitely don't have 24 hours of data
                return false;
            }

            var collectionInfo = await resp.Content.ReadFromJsonAsync<CollectionInfoResponse>(cancellationToken: ct);
            if (collectionInfo?.Result?.PointsCount == 0)
            {
                // No points in collection
                return false;
            }

            // Check if we have events from the past 24 hours
            var cutoffTime = DateTimeOffset.UtcNow.AddHours(-24);
            
            // Create a simple query to check for recent events
            var req = new ScrollRequest
            {
                Limit = 1000,
                WithPayload = true,
                Filter = new Filter
                {
                    Must = new List<Condition>
                    {
                        new Condition
                        {
                            Range = new Range
                            {
                                Key = "time",
                                Gte = cutoffTime.ToString("o")
                            }
                        }
                    }
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(req);
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var scrollResp = await _http.PostAsync($"collections/{C}/points/scroll", content, ct);
            
            if (!scrollResp.IsSuccessStatusCode)
            {
                // If scroll fails, assume we don't have enough data
                return false;
            }

            var scrollBody = await scrollResp.Content.ReadFromJsonAsync<ScrollResponse>(cancellationToken: ct);
            var recentEvents = scrollBody?.Result?.Points?.Count ?? 0;

            // We consider having 24 hours of data if we have at least some events from the past 24 hours
            // and the total collection has a reasonable number of events
            var hasRecentData = recentEvents > 0;
            var hasReasonableTotal = (collectionInfo?.Result?.PointsCount ?? 0) >= 10; // At least 10 events total

            return hasRecentData && hasReasonableTotal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking 24-hour data availability");
            // If we can't check, assume we don't have enough data
            return false;
        }
    }

    public async Task DeleteVectorsOlderThan24HoursAsync(CancellationToken ct)
    {
        try
        {
            // Get collection info to check if we have any data
            var resp = await _http.GetAsync($"collections/{C}", ct);
            if (!resp.IsSuccessStatusCode)
            {
                // Collection doesn't exist, nothing to delete
                return;
            }

            var collectionInfo = await resp.Content.ReadFromJsonAsync<CollectionInfoResponse>(cancellationToken: ct);
            if (collectionInfo?.Result?.PointsCount == 0)
            {
                // No points in collection, nothing to delete
                return;
            }

            // Calculate cutoff time (24 hours ago)
            var cutoffTime = DateTimeOffset.UtcNow.AddHours(-24);
            
            // Create a filter to find points older than 24 hours
            var deleteFilter = new Filter
            {
                Must = new List<Condition>
                {
                    new Condition
                    {
                        Range = new Range
                        {
                            Key = "time",
                            Lt = cutoffTime.ToString("o")
                        }
                    }
                }
            };

            // Create delete request
            var deleteRequest = new DeletePointsRequest
            {
                Filter = deleteFilter
            };

            var json = System.Text.Json.JsonSerializer.Serialize(deleteRequest);
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var deleteResp = await _http.PostAsync($"collections/{C}/points/delete", content, ct);
            
            if (!deleteResp.IsSuccessStatusCode)
            {
                var errorContent = await deleteResp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Failed to delete old vectors: {StatusCode} {ReasonPhrase} - {ErrorContent}", 
                    (int)deleteResp.StatusCode, deleteResp.ReasonPhrase, errorContent);
                return;
            }

            var deleteBody = await deleteResp.Content.ReadFromJsonAsync<DeletePointsResponse>(cancellationToken: ct);
            var deletedCount = deleteBody?.Result?.Status?.DeletedCount ?? 0;
            
            if (deletedCount > 0)
            {
                _logger.LogInformation("Successfully deleted {DeletedCount} vectors older than 24 hours", deletedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting vectors older than 24 hours");
        }
    }

    public async Task<IReadOnlyList<(LogEvent evt, float score)>> SearchAsync(float[] query, int k, CancellationToken ct)
    {
        var req = new SearchRequestNamed
        {
            Vector = new NamedVector { Name = "log_events", Vector = query },
            Limit = k,
            WithPayload = true
        };
        var json = System.Text.Json.JsonSerializer.Serialize(req);
        // Debug: log basic search info without sensitive payload data
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Sending search request to Qdrant with {Limit} results limit", req.Limit);
        }
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync($"collections/{C}/points/search", content, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Qdrant search failed: {(int)resp.StatusCode} {resp.ReasonPhrase} - {err}");
        }
        var body = await resp.Content.ReadFromJsonAsync<SearchResponse>(cancellationToken: ct)
                   ?? throw new InvalidOperationException("Empty search response");
        return body.Result.Select(hit =>
        {
            var pl = hit.Payload ?? new Dictionary<string, object?>();
            var timeStr = GetString(pl, "time");
            var host = GetString(pl, "host");
            var channel = GetString(pl, "channel");
            var eventId = GetInt(pl, "eventId");
            var level = GetString(pl, "level");
            var user = GetString(pl, "user");
            var message = GetString(pl, "message");
            var time = DateTimeOffset.TryParse(timeStr, out var t) ? t : DateTimeOffset.UtcNow;
            var evt = new LogEvent(time, host, channel, eventId, level, user, message);
            return (evt, (float)(hit.Score ?? 0));
        }).ToList();
    }

    private static string GetString(Dictionary<string, object?> payload, string key)
    {
        if (!payload.TryGetValue(key, out var v) || v is null)
            return string.Empty;
        if (v is string s) return s;
        if (v is System.Text.Json.JsonElement je)
        {
            if (je.ValueKind == System.Text.Json.JsonValueKind.String)
                return je.GetString() ?? string.Empty;
            return je.ToString();
        }
        return v.ToString() ?? string.Empty;
    }

    private static int GetInt(Dictionary<string, object?> payload, string key)
    {
        if (!payload.TryGetValue(key, out var v) || v is null) return 0;
        switch (v)
        {
            case int i: return i;
            case long l: return (int)l;
            case double d: return (int)d;
            case float f: return (int)f;
            case string s when int.TryParse(s, out var si): return si;
            case System.Text.Json.JsonElement je:
                if (je.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    if (je.TryGetInt32(out var n)) return n;
                    if (je.TryGetInt64(out var n64)) return (int)n64;
                    return (int)je.GetDouble();
                }
                if (je.ValueKind == System.Text.Json.JsonValueKind.String && int.TryParse(je.GetString(), out var ns))
                    return ns;
                break;
        }
        if (int.TryParse(v.ToString(), out var nFallback)) return nFallback;
        return 0;
    }

    private static string NormalizeDistance(string d)
        => d.Equals("Cosine", StringComparison.OrdinalIgnoreCase) ? "Cosine" :
           d.Equals("Dot", StringComparison.OrdinalIgnoreCase) ? "Dot" : "Cosine";

    private static string GenerateConsistentUuid(string uniqueId)
    {
        // Generate a consistent UUID based on the uniqueId string
        // This ensures the same event always gets the same UUID for deduplication
        if (string.IsNullOrEmpty(uniqueId))
        {
            return Guid.NewGuid().ToString();
        }
        
        // Use SHA256 hash of the uniqueId to generate a consistent UUID
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(uniqueId));
        
        // Take the first 16 bytes to create a UUID
        var uuidBytes = new byte[16];
        Array.Copy(hashBytes, uuidBytes, 16);
        
        // Set version (4) and variant bits for a valid UUID
        uuidBytes[6] = (byte)((uuidBytes[6] & 0x0F) | 0x40); // Version 4
        uuidBytes[8] = (byte)((uuidBytes[8] & 0x3F) | 0x80); // Variant 1
        
        return new Guid(uuidBytes).ToString();
    }

    private sealed class CreateCollectionRequest
    {
        [JsonPropertyName("vectors")] public required Dictionary<string, VectorParams> Vectors { get; set; }
    }
    private sealed class VectorParams
    {
        [JsonPropertyName("size")] public required int Size { get; set; }
        [JsonPropertyName("distance")] public required string Distance { get; set; }
    }
    private sealed class UpsertPointsRequest
    {
        [JsonPropertyName("points")] public required List<UpsertPoint> Points { get; set; }
    }
    private sealed class UpsertPoint
    {
        [JsonPropertyName("id")] public required string Id { get; set; }
        [JsonPropertyName("vectors")] public required Dictionary<string, float[]> Vectors { get; set; }
        [JsonPropertyName("payload")] public required Dictionary<string, object?> Payload { get; set; }
    }
    private sealed class SearchRequestNamed
    {
        [JsonPropertyName("vector")] public required NamedVector Vector { get; set; }
        [JsonPropertyName("limit")] public required int Limit { get; set; }
        [JsonPropertyName("with_payload")] public bool WithPayload { get; set; } = true;
    }
    private sealed class NamedVector
    {
        [JsonPropertyName("name")] public required string Name { get; set; }
        [JsonPropertyName("vector")] public required float[] Vector { get; set; }
    }
    private sealed class SearchResponse
    {
        [JsonPropertyName("result")] public required List<SearchHit> Result { get; set; }
    }
    private sealed class SearchHit
    {
        [JsonPropertyName("score")] public double? Score { get; set; }
        [JsonPropertyName("payload")] public Dictionary<string, object?>? Payload { get; set; }
    }

    private sealed class CollectionInfoResponse
    {
        [JsonPropertyName("result")] public CollectionInfo? Result { get; set; }
    }

    private sealed class CollectionInfo
    {
        [JsonPropertyName("points_count")] public int PointsCount { get; set; }
    }

    private sealed class ScrollRequest
    {
        [JsonPropertyName("limit")] public int Limit { get; set; }
        [JsonPropertyName("with_payload")] public bool WithPayload { get; set; }
        [JsonPropertyName("filter")] public Filter? Filter { get; set; }
    }

    private sealed class Filter
    {
        [JsonPropertyName("must")] public List<Condition>? Must { get; set; }
    }

    private sealed class Condition
    {
        [JsonPropertyName("range")] public Range? Range { get; set; }
    }

    private sealed class Range
    {
        [JsonPropertyName("key")] public string Key { get; set; } = "";
        [JsonPropertyName("gte")] public string Gte { get; set; } = "";
        [JsonPropertyName("lt")] public string Lt { get; set; } = "";
    }

    private sealed class ScrollResponse
    {
        [JsonPropertyName("result")] public ScrollResult? Result { get; set; }
    }

    private sealed class ScrollResult
    {
        [JsonPropertyName("points")] public List<object>? Points { get; set; }
    }

    private sealed class DeletePointsRequest
    {
        [JsonPropertyName("filter")] public Filter? Filter { get; set; }
    }

    private sealed class DeletePointsResponse
    {
        [JsonPropertyName("result")] public DeleteResult? Result { get; set; }
    }

    private sealed class DeleteResult
    {
        [JsonPropertyName("status")] public DeleteStatus? Status { get; set; }
    }

    private sealed class DeleteStatus
    {
        [JsonPropertyName("deleted_count")] public int DeletedCount { get; set; }
    }
}

public sealed class QdrantOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6333;
    public bool Https { get; set; }
    public string ApiKey { get; set; } = "";
    public bool UseCloud { get; set; }
    public string Collection { get; set; } = "log_events";
    public int VectorSize { get; set; } = 768;
    public string Distance { get; set; } = "Cosine";
}

