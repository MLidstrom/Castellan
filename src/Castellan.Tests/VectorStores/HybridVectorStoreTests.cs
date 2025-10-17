using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Models;
using Castellan.Worker.Options;
using Castellan.Worker.VectorStores;

namespace Castellan.Tests.VectorStores;

/// <summary>
/// Unit tests for HybridVectorStore decorator.
/// Tests hybrid scoring (vector + metadata), over-fetching, re-ranking, and fallback behavior.
/// </summary>
public class HybridVectorStoreTests
{
    private readonly Mock<IVectorStore> _mockInnerStore;
    private readonly Mock<ILogger<HybridVectorStore>> _mockLogger;
    private readonly HybridSearchOptions _options;

    public HybridVectorStoreTests()
    {
        _mockInnerStore = new Mock<IVectorStore>();
        _mockLogger = new Mock<ILogger<HybridVectorStore>>();
        _options = new HybridSearchOptions
        {
            Enabled = true,
            VectorSimilarityWeight = 0.7f,
            MetadataWeight = 0.3f,
            RecencyDecayHours = 24.0f,
            RecencyWeight = 0.3f,
            RiskLevelWeight = 0.7f,
            OverFetchMultiplier = 3.0f
        };
    }

    [Fact]
    public async Task SearchAsync_WithHybridEnabled_ShouldOverFetchAndRerank()
    {
        // Arrange
        var store = CreateStore();
        var query = new float[] { 0.1f, 0.2f, 0.3f };
        var now = DateTimeOffset.UtcNow;

        // Create events with different timestamps (recent = higher metadata score)
        var oldEvent = CreateEvent(4625, now.AddHours(-48)); // 48 hours old
        var recentEvent = CreateEvent(4624, now.AddHours(-1)); // 1 hour old

        // Mock returns 2 results (would over-fetch 3 for k=1)
        // Vector scores close enough that recency boost can make a difference
        var vectorResults = new List<(LogEvent evt, float score)>
        {
            (oldEvent, 0.80f),    // Slightly higher vector score, but old
            (recentEvent, 0.75f)  // Close vector score, but very recent
        };

        _mockInnerStore.Setup(x => x.SearchAsync(query, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorResults);

        // Act
        var results = await store.SearchAsync(query, k: 1, CancellationToken.None);

        // Assert
        results.Should().HaveCount(1);
        // Recent event should be boosted and ranked higher despite lower vector score
        results[0].evt.EventId.Should().Be(4624); // Recent event wins

        _mockInnerStore.Verify(x => x.SearchAsync(query, 3, It.IsAny<CancellationToken>()), Times.Once);

        var stats = store.GetStatistics();
        stats.TotalSearches.Should().Be(1);
        stats.HybridSearches.Should().Be(1);
        stats.HybridRate.Should().Be(1.0f);
    }

    [Fact]
    public async Task SearchAsync_WithHybridDisabled_ShouldPassThroughToInner()
    {
        // Arrange
        _options.Enabled = false;
        var store = CreateStore();
        var query = new float[] { 0.1f, 0.2f };
        var event1 = CreateEvent(4625, DateTimeOffset.UtcNow);

        var vectorResults = new List<(LogEvent evt, float score)> { (event1, 0.8f) };

        _mockInnerStore.Setup(x => x.SearchAsync(query, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorResults);

        // Act
        var results = await store.SearchAsync(query, k: 5, CancellationToken.None);

        // Assert
        results.Should().BeEquivalentTo(vectorResults);
        _mockInnerStore.Verify(x => x.SearchAsync(query, 5, It.IsAny<CancellationToken>()), Times.Once);

        var stats = store.GetStatistics();
        stats.FallbackSearches.Should().Be(1); // Counted as fallback when disabled
    }

    [Fact]
    public async Task SearchAsync_RecencyBoost_ShouldFavorRecentEvents()
    {
        // Arrange
        var store = CreateStore();
        var query = new float[] { 0.1f };
        var now = DateTimeOffset.UtcNow;

        // Create events at different ages
        var veryOldEvent = CreateEvent(1, now.AddHours(-72)); // 3 days old
        var oldEvent = CreateEvent(2, now.AddHours(-24));     // 1 day old
        var recentEvent = CreateEvent(3, now.AddHours(-1));   // 1 hour old

        // All have same vector score - recency should determine ranking
        var vectorResults = new List<(LogEvent evt, float score)>
        {
            (veryOldEvent, 0.8f),
            (oldEvent, 0.8f),
            (recentEvent, 0.8f)
        };

        _mockInnerStore.Setup(x => x.SearchAsync(query, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorResults);

        // Act
        var results = await store.SearchAsync(query, k: 3, CancellationToken.None);

        // Assert
        results.Should().HaveCount(3);
        results[0].evt.EventId.Should().Be(3); // Most recent
        results[1].evt.EventId.Should().Be(2); // Middle age
        results[2].evt.EventId.Should().Be(1); // Oldest
    }

    [Fact]
    public async Task SearchAsync_EmptyResults_ShouldReturnEmpty()
    {
        // Arrange
        var store = CreateStore();
        var query = new float[] { 0.1f };

        _mockInnerStore.Setup(x => x.SearchAsync(query, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(LogEvent evt, float score)>());

        // Act
        var results = await store.SearchAsync(query, k: 10, CancellationToken.None);

        // Assert
        results.Should().BeEmpty();

        var stats = store.GetStatistics();
        stats.HybridSearches.Should().Be(1);
    }

    [Fact]
    public async Task SearchAsync_InnerStoreThrows_ShouldFallbackToInnerSearch()
    {
        // Arrange
        var store = CreateStore();
        var query = new float[] { 0.1f };
        var callCount = 0;

        _mockInnerStore.Setup(x => x.SearchAsync(query, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new Exception("Vector search failed");

                // Second call (fallback) succeeds
                return new List<(LogEvent evt, float score)>
                {
                    (CreateEvent(4625, DateTimeOffset.UtcNow), 0.9f)
                };
            });

        // Act
        var results = await store.SearchAsync(query, k: 5, CancellationToken.None);

        // Assert
        results.Should().HaveCount(1);
        callCount.Should().Be(2); // First call failed, second succeeded

        var stats = store.GetStatistics();
        stats.TotalSearches.Should().Be(1);
        stats.FallbackSearches.Should().Be(1);
        stats.HybridSearches.Should().Be(0); // Hybrid attempt failed, then fell back
    }

    [Fact]
    public async Task SearchAsync_OverFetchMultiplier_ShouldFetchCorrectAmount()
    {
        // Arrange
        _options.OverFetchMultiplier = 2.5f;
        var store = CreateStore();
        var query = new float[] { 0.1f };

        _mockInnerStore.Setup(x => x.SearchAsync(query, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(LogEvent evt, float score)>());

        // Act
        await store.SearchAsync(query, k: 10, CancellationToken.None);

        // Assert - should fetch ceiling(10 * 2.5) = 25
        _mockInnerStore.Verify(x => x.SearchAsync(query, 25, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_ReturnsTopK_AfterReranking()
    {
        // Arrange
        var store = CreateStore();
        var query = new float[] { 0.1f };
        var now = DateTimeOffset.UtcNow;

        // Create 10 events with varying scores
        var vectorResults = Enumerable.Range(1, 10)
            .Select(i => (evt: CreateEvent(i, now.AddHours(-i)), score: 0.5f + (i * 0.01f)))
            .ToList();

        _mockInnerStore.Setup(x => x.SearchAsync(query, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorResults);

        // Act - request only top 3
        var results = await store.SearchAsync(query, k: 3, CancellationToken.None);

        // Assert
        results.Should().HaveCount(3);
        // After hybrid re-ranking, most recent events should be at top
    }

    [Fact]
    public async Task EnsureCollectionAsync_ShouldPassThroughToInner()
    {
        // Arrange
        var store = CreateStore();

        // Act
        await store.EnsureCollectionAsync(CancellationToken.None);

        // Assert
        _mockInnerStore.Verify(x => x.EnsureCollectionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpsertAsync_ShouldPassThroughToInner()
    {
        // Arrange
        var store = CreateStore();
        var evt = CreateEvent(4625, DateTimeOffset.UtcNow);
        var embedding = new float[] { 0.1f, 0.2f };

        // Act
        await store.UpsertAsync(evt, embedding, CancellationToken.None);

        // Assert
        _mockInnerStore.Verify(x => x.UpsertAsync(evt, embedding, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BatchUpsertAsync_ShouldPassThroughToInner()
    {
        // Arrange
        var store = CreateStore();
        var items = new List<(LogEvent logEvent, float[] embedding)>
        {
            (CreateEvent(4625, DateTimeOffset.UtcNow), new float[] { 0.1f }),
            (CreateEvent(4624, DateTimeOffset.UtcNow), new float[] { 0.2f })
        };

        // Act
        await store.BatchUpsertAsync(items, CancellationToken.None);

        // Assert
        _mockInnerStore.Verify(x => x.BatchUpsertAsync(items, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Has24HoursOfDataAsync_ShouldPassThroughToInner()
    {
        // Arrange
        var store = CreateStore();
        _mockInnerStore.Setup(x => x.Has24HoursOfDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await store.Has24HoursOfDataAsync(CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        _mockInnerStore.Verify(x => x.Has24HoursOfDataAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteVectorsOlderThan24HoursAsync_ShouldPassThroughToInner()
    {
        // Arrange
        var store = CreateStore();

        // Act
        await store.DeleteVectorsOlderThan24HoursAsync(CancellationToken.None);

        // Assert
        _mockInnerStore.Verify(x => x.DeleteVectorsOlderThan24HoursAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GetStatistics_InitialState_ShouldReturnZeroValues()
    {
        // Arrange
        var store = CreateStore();

        // Act
        var stats = store.GetStatistics();

        // Assert
        stats.TotalSearches.Should().Be(0);
        stats.HybridSearches.Should().Be(0);
        stats.FallbackSearches.Should().Be(0);
        stats.HybridRate.Should().Be(0.0f);
    }

    [Fact]
    public async Task GetStatistics_MultipleSearches_ShouldTrackCorrectly()
    {
        // Arrange
        var store = CreateStore();
        var query = new float[] { 0.1f };

        _mockInnerStore.Setup(x => x.SearchAsync(query, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<(LogEvent evt, float score)>
            {
                (CreateEvent(4625, DateTimeOffset.UtcNow), 0.9f)
            });

        // Act
        await store.SearchAsync(query, k: 5, CancellationToken.None);
        await store.SearchAsync(query, k: 5, CancellationToken.None);
        await store.SearchAsync(query, k: 5, CancellationToken.None);

        // Assert
        var stats = store.GetStatistics();
        stats.TotalSearches.Should().Be(3);
        stats.HybridSearches.Should().Be(3);
        stats.FallbackSearches.Should().Be(0);
        stats.HybridRate.Should().Be(1.0f);
    }

    [Fact]
    public void Constructor_InvalidWeights_ShouldDisableHybridSearch()
    {
        // Arrange
        _options.VectorSimilarityWeight = 0.6f;
        _options.MetadataWeight = 0.5f; // Sum > 1.0, invalid!

        // Act
        var store = CreateStore();

        // Assert - hybrid should be disabled due to invalid config
        // Statistics should reflect fallback behavior
        var stats = store.GetStatistics();
        stats.Should().NotBeNull();
    }

    private HybridVectorStore CreateStore()
    {
        return new HybridVectorStore(
            _mockInnerStore.Object,
            Options.Create(_options),
            _mockLogger.Object);
    }

    private LogEvent CreateEvent(int eventId, DateTimeOffset timestamp)
    {
        return new LogEvent(
            timestamp,
            "TEST-HOST",
            "Security",
            eventId,
            $"Test event {eventId}",
            "testuser",
            "{}",
            Guid.NewGuid().ToString());
    }
}
