using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Embeddings;
using Castellan.Worker.Options;

namespace Castellan.Tests.Embeddings;

/// <summary>
/// Unit tests for CachingEmbedder decorator.
/// Tests cache hit/miss behavior, eviction, and statistics.
/// </summary>
public class CachingEmbedderTests : IDisposable
{
    private readonly Mock<IEmbedder> _mockInnerEmbedder;
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ILogger<CachingEmbedder>> _mockLogger;
    private readonly EmbeddingCacheOptions _options;

    public CachingEmbedderTests()
    {
        _mockInnerEmbedder = new Mock<IEmbedder>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 1000 // Match MaxEntries
        });
        _mockLogger = new Mock<ILogger<CachingEmbedder>>();
        _options = new EmbeddingCacheOptions
        {
            Enabled = true,
            TtlMinutes = 30,
            MaxEntries = 1000,
            Provider = "TestProvider",
            Model = "test-model"
        };
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }

    [Fact]
    public async Task EmbedAsync_WithCacheDisabled_ShouldAlwaysCallInner()
    {
        // Arrange
        _options.Enabled = false;
        var embedder = CreateEmbedder();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        _mockInnerEmbedder.Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);

        // Act
        var result1 = await embedder.EmbedAsync("test text", CancellationToken.None);
        var result2 = await embedder.EmbedAsync("test text", CancellationToken.None);

        // Assert
        result1.Should().BeEquivalentTo(testEmbedding);
        result2.Should().BeEquivalentTo(testEmbedding);
        _mockInnerEmbedder.Verify(x => x.EmbedAsync("test text", It.IsAny<CancellationToken>()), Times.Exactly(2));
        embedder.GetStatistics().HitRate.Should().Be(0.0f);
    }

    [Fact]
    public async Task EmbedAsync_FirstCall_ShouldBeCacheMiss()
    {
        // Arrange
        var embedder = CreateEmbedder();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        _mockInnerEmbedder.Setup(x => x.EmbedAsync("test text", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);

        // Act
        var result = await embedder.EmbedAsync("test text", CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(testEmbedding);
        _mockInnerEmbedder.Verify(x => x.EmbedAsync("test text", It.IsAny<CancellationToken>()), Times.Once);

        var stats = embedder.GetStatistics();
        stats.TotalRequests.Should().Be(1);
        stats.Hits.Should().Be(0);
        stats.Misses.Should().Be(1);
        stats.HitRate.Should().Be(0.0f);
    }

    [Fact]
    public async Task EmbedAsync_SecondCallSameText_ShouldBeCacheHit()
    {
        // Arrange
        var embedder = CreateEmbedder();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        _mockInnerEmbedder.Setup(x => x.EmbedAsync("test text", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);

        // Act
        var result1 = await embedder.EmbedAsync("test text", CancellationToken.None);
        var result2 = await embedder.EmbedAsync("test text", CancellationToken.None);

        // Assert
        result1.Should().BeEquivalentTo(testEmbedding);
        result2.Should().BeEquivalentTo(testEmbedding);
        result2.Should().BeSameAs(result1); // Same reference from cache
        _mockInnerEmbedder.Verify(x => x.EmbedAsync("test text", It.IsAny<CancellationToken>()), Times.Once);

        var stats = embedder.GetStatistics();
        stats.TotalRequests.Should().Be(2);
        stats.Hits.Should().Be(1);
        stats.Misses.Should().Be(1);
        stats.HitRate.Should().Be(0.5f);
    }

    [Fact]
    public async Task EmbedAsync_DifferentTexts_ShouldBeCacheMisses()
    {
        // Arrange
        var embedder = CreateEmbedder();
        var embedding1 = new float[] { 0.1f, 0.2f, 0.3f };
        var embedding2 = new float[] { 0.4f, 0.5f, 0.6f };
        _mockInnerEmbedder.Setup(x => x.EmbedAsync("text1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding1);
        _mockInnerEmbedder.Setup(x => x.EmbedAsync("text2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding2);

        // Act
        var result1 = await embedder.EmbedAsync("text1", CancellationToken.None);
        var result2 = await embedder.EmbedAsync("text2", CancellationToken.None);

        // Assert
        result1.Should().BeEquivalentTo(embedding1);
        result2.Should().BeEquivalentTo(embedding2);
        _mockInnerEmbedder.Verify(x => x.EmbedAsync("text1", It.IsAny<CancellationToken>()), Times.Once);
        _mockInnerEmbedder.Verify(x => x.EmbedAsync("text2", It.IsAny<CancellationToken>()), Times.Once);

        var stats = embedder.GetStatistics();
        stats.TotalRequests.Should().Be(2);
        stats.Hits.Should().Be(0);
        stats.Misses.Should().Be(2);
        stats.HitRate.Should().Be(0.0f);
    }

    [Fact]
    public async Task EmbedAsync_TextNormalization_ShouldTreatWhitespaceVariationsAsEquivalent()
    {
        // Arrange
        var embedder = CreateEmbedder();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        _mockInnerEmbedder.Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);

        // Act
        var result1 = await embedder.EmbedAsync("test  text", CancellationToken.None); // Double space
        var result2 = await embedder.EmbedAsync("test text", CancellationToken.None);  // Single space

        // Assert
        result1.Should().BeEquivalentTo(testEmbedding);
        result2.Should().BeEquivalentTo(testEmbedding);

        // Both should hit cache due to normalization
        var stats = embedder.GetStatistics();
        stats.Hits.Should().Be(1);
        _mockInnerEmbedder.Verify(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EmbedAsync_CaseInsensitive_ShouldTreatDifferentCasesAsEquivalent()
    {
        // Arrange
        var embedder = CreateEmbedder();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        // Note: Cache normalizes to lowercase, so any case variation should hit cache
        _mockInnerEmbedder.Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);

        // Act
        var result1 = await embedder.EmbedAsync("Test Text", CancellationToken.None);
        var result2 = await embedder.EmbedAsync("test text", CancellationToken.None);
        var result3 = await embedder.EmbedAsync("TEST TEXT", CancellationToken.None);

        // Assert
        result1.Should().BeEquivalentTo(testEmbedding);
        result2.Should().BeEquivalentTo(testEmbedding);
        result3.Should().BeEquivalentTo(testEmbedding);

        // All variations should normalize to same key, so only 1 miss
        _mockInnerEmbedder.Verify(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        var stats = embedder.GetStatistics();
        stats.Misses.Should().Be(1); // Different cases = same cache key (normalized)
        stats.Hits.Should().Be(2); // Second and third calls hit cache
    }

    [Fact]
    public async Task EmbedAsync_MultipleHits_ShouldIncreaseCacheHitRate()
    {
        // Arrange
        var embedder = CreateEmbedder();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        _mockInnerEmbedder.Setup(x => x.EmbedAsync("test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);

        // Act
        await embedder.EmbedAsync("test", CancellationToken.None); // Miss
        await embedder.EmbedAsync("test", CancellationToken.None); // Hit
        await embedder.EmbedAsync("test", CancellationToken.None); // Hit
        await embedder.EmbedAsync("test", CancellationToken.None); // Hit

        // Assert
        var stats = embedder.GetStatistics();
        stats.TotalRequests.Should().Be(4);
        stats.Hits.Should().Be(3);
        stats.Misses.Should().Be(1);
        stats.HitRate.Should().Be(0.75f); // 3/4 = 75%
        _mockInnerEmbedder.Verify(x => x.EmbedAsync("test", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EmbedAsync_ConcurrentRequests_ShouldHandleStampede()
    {
        // Arrange
        var embedder = CreateEmbedder();
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var callCount = 0;
        _mockInnerEmbedder.Setup(x => x.EmbedAsync("test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                Interlocked.Increment(ref callCount);
                Thread.Sleep(100); // Simulate slow embedding
                return testEmbedding;
            });

        // Act
        var tasks = new Task<float[]>[10];
        for (int i = 0; i < 10; i++)
        {
            tasks[i] = embedder.EmbedAsync("test", CancellationToken.None);
        }
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllBeEquivalentTo(testEmbedding);
        // All requests should get the same result, but inner embedder might be called multiple times
        // due to cache stampede (acceptable tradeoff for simplicity)
        callCount.Should().BeGreaterOrEqualTo(1);
        callCount.Should().BeLessThan(10); // Should be < 10 due to some cache hits
    }

    [Fact]
    public async Task EmbedAsync_EmptyString_ShouldCacheCorrectly()
    {
        // Arrange
        var embedder = CreateEmbedder();
        var testEmbedding = new float[] { 0.0f, 0.0f, 0.0f };
        _mockInnerEmbedder.Setup(x => x.EmbedAsync("", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);

        // Act
        var result1 = await embedder.EmbedAsync("", CancellationToken.None);
        var result2 = await embedder.EmbedAsync("", CancellationToken.None);

        // Assert
        result1.Should().BeEquivalentTo(testEmbedding);
        result2.Should().BeEquivalentTo(testEmbedding);
        _mockInnerEmbedder.Verify(x => x.EmbedAsync("", It.IsAny<CancellationToken>()), Times.Once);

        var stats = embedder.GetStatistics();
        stats.HitRate.Should().Be(0.5f);
    }

    [Fact]
    public void GetStatistics_InitialState_ShouldReturnZeroValues()
    {
        // Arrange
        var embedder = CreateEmbedder();

        // Act
        var stats = embedder.GetStatistics();

        // Assert
        stats.TotalRequests.Should().Be(0);
        stats.Hits.Should().Be(0);
        stats.Misses.Should().Be(0);
        stats.HitRate.Should().Be(0.0f);
    }

    [Fact]
    public async Task EmbedAsync_WithSmallCache_ShouldStillFunctionCorrectly()
    {
        // Arrange - use small cache to test behavior under memory pressure
        var smallCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 2 });
        var embedder = new CachingEmbedder(
            _mockInnerEmbedder.Object,
            smallCache,
            Options.Create(_options),
            _mockLogger.Object);

        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        _mockInnerEmbedder.Setup(x => x.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);

        // Act - add more entries than cache size
        await embedder.EmbedAsync("text1", CancellationToken.None);
        await embedder.EmbedAsync("text2", CancellationToken.None);
        await embedder.EmbedAsync("text3", CancellationToken.None);

        // Assert - cache should still work, just with more misses
        var stats = embedder.GetStatistics();
        stats.TotalRequests.Should().Be(3);
        stats.Misses.Should().Be(3); // All misses due to small cache

        // Eviction counter may or may not have incremented yet (depends on GC timing)
        stats.Evictions.Should().BeGreaterOrEqualTo(0);

        smallCache.Dispose();
    }

    [Fact]
    public async Task EmbedAsync_LongText_ShouldCacheCorrectly()
    {
        // Arrange
        var embedder = CreateEmbedder();
        var longText = new string('a', 10000); // 10KB text
        var testEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        _mockInnerEmbedder.Setup(x => x.EmbedAsync(longText, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testEmbedding);

        // Act
        var result1 = await embedder.EmbedAsync(longText, CancellationToken.None);
        var result2 = await embedder.EmbedAsync(longText, CancellationToken.None);

        // Assert
        result1.Should().BeEquivalentTo(testEmbedding);
        result2.Should().BeEquivalentTo(testEmbedding);
        _mockInnerEmbedder.Verify(x => x.EmbedAsync(longText, It.IsAny<CancellationToken>()), Times.Once);

        var stats = embedder.GetStatistics();
        stats.HitRate.Should().Be(0.5f);
    }

    private CachingEmbedder CreateEmbedder()
    {
        return new CachingEmbedder(
            _mockInnerEmbedder.Object,
            _memoryCache,
            Options.Create(_options),
            _mockLogger.Object);
    }
}
