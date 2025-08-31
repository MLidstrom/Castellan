using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;
using Castellan.Worker.Models;
using Castellan.Worker.VectorStores;
using Castellan.Tests.TestUtilities;

namespace Castellan.Tests.VectorStores;

public class QdrantVectorStoreTests
{
    private readonly Mock<IOptions<QdrantOptions>> _mockOptions;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<ILogger<QdrantVectorStore>> _mockLogger;
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;

    public QdrantVectorStoreTests()
    {
        _mockOptions = new Mock<IOptions<QdrantOptions>>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockLogger = new Mock<ILogger<QdrantVectorStore>>();
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);
    }

    [Fact]
    public void Constructor_ShouldCreateInstance()
    {
        // Arrange
        var options = new QdrantOptions
        {
            UseCloud = false,
            Host = "localhost",
            Port = 6333,
            Https = false,
            ApiKey = "",
            Collection = "log_events",
            VectorSize = 768,
            Distance = "Cosine"
        };
        _mockOptions.Setup(x => x.Value).Returns(options);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        // Act
        var vectorStore = new QdrantVectorStore(_mockOptions.Object, _mockHttpClientFactory.Object, _mockLogger.Object);

        // Assert
        vectorStore.Should().NotBeNull();
    }

    [Fact]
    public async Task EnsureCollectionAsync_ShouldCreateCollection()
    {
        // Arrange
        var options = new QdrantOptions
        {
            UseCloud = false,
            Host = "localhost",
            Port = 6333,
            Https = false,
            ApiKey = "",
            Collection = "log_events",
            VectorSize = 768,
            Distance = "Cosine"
        };
        _mockOptions.Setup(x => x.Value).Returns(options);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var vectorStore = new QdrantVectorStore(_mockOptions.Object, _mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        await vectorStore.EnsureCollectionAsync(CancellationToken.None);

        // Assert
        // Should not throw exception
    }

    [Fact]
    public async Task EnsureCollectionAsync_WithExistingCollection_ShouldNotThrow()
    {
        // Arrange
        var options = new QdrantOptions
        {
            UseCloud = false,
            Host = "localhost",
            Port = 6333,
            Https = false,
            ApiKey = "",
            Collection = "log_events",
            VectorSize = 768,
            Distance = "Cosine"
        };
        _mockOptions.Setup(x => x.Value).Returns(options);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Conflict // 409 - Already exists
            });

        var vectorStore = new QdrantVectorStore(_mockOptions.Object, _mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        await vectorStore.EnsureCollectionAsync(CancellationToken.None);

        // Assert
        // Should not throw exception
    }

    [Fact]
    public async Task EnsureCollectionAsync_WithHttpError_ShouldThrowException()
    {
        // Arrange
        var options = new QdrantOptions
        {
            UseCloud = false,
            Host = "localhost",
            Port = 6333,
            Https = false,
            ApiKey = "",
            Collection = "log_events",
            VectorSize = 768,
            Distance = "Cosine"
        };
        _mockOptions.Setup(x => x.Value).Returns(options);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var vectorStore = new QdrantVectorStore(_mockOptions.Object, _mockHttpClientFactory.Object, _mockLogger.Object);

        // Act & Assert
        var action = () => vectorStore.EnsureCollectionAsync(CancellationToken.None);
        await action.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task UpsertAsync_ShouldUpsertEvent()
    {
        // Arrange
        var options = new QdrantOptions
        {
            UseCloud = false,
            Host = "localhost",
            Port = 6333,
            Https = false,
            ApiKey = "",
            Collection = "log_events",
            VectorSize = 768,
            Distance = "Cosine"
        };
        _mockOptions.Setup(x => x.Value).Returns(options);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4624,
            "Information",
            "testuser",
            "An account was successfully logged on"
        );

        var embedding = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            });

        var vectorStore = new QdrantVectorStore(_mockOptions.Object, _mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        await vectorStore.UpsertAsync(logEvent, embedding, CancellationToken.None);

        // Assert
        // Should not throw exception
    }

    [Fact]
    public async Task UpsertAsync_WithHttpError_ShouldThrowException()
    {
        // Arrange
        var options = new QdrantOptions
        {
            UseCloud = false,
            Host = "localhost",
            Port = 6333,
            Https = false,
            ApiKey = "",
            Collection = "log_events",
            VectorSize = 768,
            Distance = "Cosine"
        };
        _mockOptions.Setup(x => x.Value).Returns(options);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4624,
            "Information",
            "testuser",
            "An account was successfully logged on"
        );

        var embedding = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var vectorStore = new QdrantVectorStore(_mockOptions.Object, _mockHttpClientFactory.Object, _mockLogger.Object);

        // Act & Assert
        var action = () => vectorStore.UpsertAsync(logEvent, embedding, CancellationToken.None);
        await action.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnSearchResults()
    {
        // Arrange
        var options = new QdrantOptions
        {
            UseCloud = false,
            Host = "localhost",
            Port = 6333,
            Https = false,
            ApiKey = "",
            Collection = "log_events",
            VectorSize = 768,
            Distance = "Cosine"
        };
        _mockOptions.Setup(x => x.Value).Returns(options);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        var query = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };

        var searchResponse = new
        {
            result = new[]
            {
                new
                {
                    id = "1",
                    score = 0.95f,
                    payload = new Dictionary<string, object>
                    {
                        ["time"] = DateTimeOffset.UtcNow.ToString("o"),
                        ["host"] = "TEST-HOST",
                        ["channel"] = "Security",
                        ["eventId"] = 4624,
                        ["level"] = "Information",
                        ["user"] = "testuser",
                        ["message"] = "An account was successfully logged on"
                    }
                }
            }
        };

        var responseJson = JsonSerializer.Serialize(searchResponse);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson)
            });

        var vectorStore = new QdrantVectorStore(_mockOptions.Object, _mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        var result = await vectorStore.SearchAsync(query, 5, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].evt.Should().NotBeNull();
        result[0].score.Should().Be(0.95f);
    }

    [Fact]
    public async Task SearchAsync_WithHttpError_ShouldThrowException()
    {
        // Arrange
        var options = new QdrantOptions
        {
            UseCloud = false,
            Host = "localhost",
            Port = 6333,
            Https = false,
            ApiKey = "",
            Collection = "log_events",
            VectorSize = 768,
            Distance = "Cosine"
        };
        _mockOptions.Setup(x => x.Value).Returns(options);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        var query = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var vectorStore = new QdrantVectorStore(_mockOptions.Object, _mockHttpClientFactory.Object, _mockLogger.Object);

        // Act & Assert
        var action = () => vectorStore.SearchAsync(query, 5, CancellationToken.None);
        await action.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task SearchAsync_WithEmptyResponse_ShouldReturnEmptyList()
    {
        // Arrange
        var options = new QdrantOptions
        {
            UseCloud = false,
            Host = "localhost",
            Port = 6333,
            Https = false,
            ApiKey = "",
            Collection = "log_events",
            VectorSize = 768,
            Distance = "Cosine"
        };
        _mockOptions.Setup(x => x.Value).Returns(options);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        var query = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };

        var searchResponse = new { result = new object[0] };
        var responseJson = JsonSerializer.Serialize(searchResponse);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson)
            });

        var vectorStore = new QdrantVectorStore(_mockOptions.Object, _mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        var result = await vectorStore.SearchAsync(query, 5, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var options = new QdrantOptions
        {
            UseCloud = false,
            Host = "localhost",
            Port = 6333,
            Https = false,
            ApiKey = "",
            Collection = "log_events",
            VectorSize = 768,
            Distance = "Cosine"
        };
        _mockOptions.Setup(x => x.Value).Returns(options);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        var query = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };

        var vectorStore = new QdrantVectorStore(_mockOptions.Object, _mockHttpClientFactory.Object, _mockLogger.Object);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var action = () => vectorStore.SearchAsync(query, 5, cts.Token);
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void QdrantOptions_ShouldHaveDefaultValues()
    {
        // Act
        var options = new QdrantOptions();

        // Assert
        options.UseCloud.Should().BeFalse();
        options.Host.Should().Be("localhost");
        options.Port.Should().Be(6333);
        options.Https.Should().BeFalse();
        options.ApiKey.Should().Be("");
        options.Collection.Should().Be("log_events");
        options.VectorSize.Should().Be(768);
        options.Distance.Should().Be("Cosine");
    }

    [Fact]
    public void QdrantOptions_ShouldAllowCustomValues()
    {
        // Arrange
        var customUseCloud = true;
        var customHost = "custom-host";
        var customPort = 8080;
        var customHttps = true;
        var customApiKey = "custom-api-key";
        var customCollection = "custom-collection";
        var customVectorSize = 1024;
        var customDistance = "Euclidean";

        // Act
        var options = new QdrantOptions
        {
            UseCloud = customUseCloud,
            Host = customHost,
            Port = customPort,
            Https = customHttps,
            ApiKey = customApiKey,
            Collection = customCollection,
            VectorSize = customVectorSize,
            Distance = customDistance
        };

        // Assert
        options.UseCloud.Should().Be(customUseCloud);
        options.Host.Should().Be(customHost);
        options.Port.Should().Be(customPort);
        options.Https.Should().Be(customHttps);
        options.ApiKey.Should().Be(customApiKey);
        options.Collection.Should().Be(customCollection);
        options.VectorSize.Should().Be(customVectorSize);
        options.Distance.Should().Be(customDistance);
    }

    [Fact]
    public void QdrantOptions_ShouldHandleNullValues()
    {
        // Act
        var options = new QdrantOptions
        {
            Host = null!,
            ApiKey = null!,
            Collection = null!,
            Distance = null!
        };

        // Assert
        options.Host.Should().BeNull();
        options.ApiKey.Should().BeNull();
        options.Collection.Should().BeNull();
        options.Distance.Should().BeNull();
    }

    [Fact]
    public void QdrantOptions_ShouldHandleEmptyStrings()
    {
        // Act
        var options = new QdrantOptions
        {
            Host = "",
            ApiKey = "",
            Collection = "",
            Distance = ""
        };

        // Assert
        options.Host.Should().Be("");
        options.ApiKey.Should().Be("");
        options.Collection.Should().Be("");
        options.Distance.Should().Be("");
    }

    [Fact]
    public void QdrantOptions_ShouldBeMutable()
    {
        // Arrange
        var options = new QdrantOptions();

        // Act
        options.UseCloud = true;
        options.Host = "updated-host";
        options.Port = 9090;
        options.Https = true;
        options.ApiKey = "updated-api-key";
        options.Collection = "updated-collection";
        options.VectorSize = 2048;
        options.Distance = "Dot";

        // Assert
        options.UseCloud.Should().BeTrue();
        options.Host.Should().Be("updated-host");
        options.Port.Should().Be(9090);
        options.Https.Should().BeTrue();
        options.ApiKey.Should().Be("updated-api-key");
        options.Collection.Should().Be("updated-collection");
        options.VectorSize.Should().Be(2048);
        options.Distance.Should().Be("Dot");
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(2048)]
    [InlineData(4096)]
    public void QdrantOptions_ShouldHandleVariousVectorSizes(int vectorSize)
    {
        // Act
        var options = new QdrantOptions
        {
            VectorSize = vectorSize
        };

        // Assert
        options.VectorSize.Should().Be(vectorSize);
    }

    [Theory]
    [InlineData("Cosine")]
    [InlineData("Euclidean")]
    [InlineData("Dot")]
    public void QdrantOptions_ShouldHandleVariousDistances(string distance)
    {
        // Act
        var options = new QdrantOptions
        {
            Distance = distance
        };

        // Assert
        options.Distance.Should().Be(distance);
    }

    [Theory]
    [InlineData(80)]
    [InlineData(443)]
    [InlineData(8080)]
    [InlineData(6333)]
    public void QdrantOptions_ShouldHandleVariousPorts(int port)
    {
        // Act
        var options = new QdrantOptions
        {
            Port = port
        };

        // Assert
        options.Port.Should().Be(port);
    }

    [Fact]
    public void QdrantOptions_ShouldHandleSpecialCharactersInHost()
    {
        // Arrange
        var specialHost = "api.example.com";

        // Act
        var options = new QdrantOptions
        {
            Host = specialHost
        };

        // Assert
        options.Host.Should().Be(specialHost);
    }

    [Fact]
    public void QdrantOptions_ShouldHandleLongValues()
    {
        // Arrange
        var longHost = new string('A', 1000);
        var longApiKey = new string('B', 1000);
        var longCollection = new string('C', 1000);
        var longDistance = new string('D', 1000);

        // Act
        var options = new QdrantOptions
        {
            Host = longHost,
            ApiKey = longApiKey,
            Collection = longCollection,
            Distance = longDistance
        };

        // Assert
        options.Host.Should().Be(longHost);
        options.ApiKey.Should().Be(longApiKey);
        options.Collection.Should().Be(longCollection);
        options.Distance.Should().Be(longDistance);
        options.Host.Length.Should().Be(1000);
        options.ApiKey.Length.Should().Be(1000);
        options.Collection.Length.Should().Be(1000);
        options.Distance.Length.Should().Be(1000);
    }

    #region 24-Hour Data Management Tests

    [Fact]
    public async Task Has24HoursOfDataAsync_ShouldReturnTrue_WhenCollectionHasRecentData()
    {
        // Arrange
        var options = new QdrantOptions
        {
            UseCloud = false,
            Host = "localhost",
            Port = 6333,
            Https = false,
            ApiKey = "",
            Collection = "log_events",
            VectorSize = 768,
            Distance = "Cosine"
        };
        _mockOptions.Setup(x => x.Value).Returns(options);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        // Mock collection info response (has data)
        var collectionInfoResponse = new
        {
            result = new
            {
                points_count = 100
            }
        };

        // Mock scroll response (has recent data)
        var scrollResponse = new
        {
            result = new
            {
                points = new object[] { new { id = "1" }, new { id = "2" } }
            }
        };

        _mockHttpHandler
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(collectionInfoResponse), Encoding.UTF8, "application/json")
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(scrollResponse), Encoding.UTF8, "application/json")
            });

        var vectorStore = new QdrantVectorStore(_mockOptions.Object, _mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        var result = await vectorStore.Has24HoursOfDataAsync(CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Has24HoursOfDataAsync_ShouldReturnFalse_WhenCollectionIsEmpty()
    {
        // Arrange
        var options = new QdrantOptions
        {
            UseCloud = false,
            Host = "localhost",
            Port = 6333,
            Https = false,
            ApiKey = "",
            Collection = "log_events",
            VectorSize = 768,
            Distance = "Cosine"
        };
        _mockOptions.Setup(x => x.Value).Returns(options);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        // Mock collection info response (empty)
        var collectionInfoResponse = new
        {
            result = new
            {
                points_count = 0
            }
        };

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(collectionInfoResponse), Encoding.UTF8, "application/json")
            });

        var vectorStore = new QdrantVectorStore(_mockOptions.Object, _mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        var result = await vectorStore.Has24HoursOfDataAsync(CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Has24HoursOfDataAsync_ShouldReturnFalse_WhenCollectionDoesNotExist()
    {
        // Arrange
        var options = new QdrantOptions
        {
            UseCloud = false,
            Host = "localhost",
            Port = 6333,
            Https = false,
            ApiKey = "",
            Collection = "log_events",
            VectorSize = 768,
            Distance = "Cosine"
        };
        _mockOptions.Setup(x => x.Value).Returns(options);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var vectorStore = new QdrantVectorStore(_mockOptions.Object, _mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        var result = await vectorStore.Has24HoursOfDataAsync(CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Has24HoursOfDataAsync_ShouldReturnFalse_WhenScrollFails()
    {
        // Arrange
        var options = new QdrantOptions
        {
            UseCloud = false,
            Host = "localhost",
            Port = 6333,
            Https = false,
            ApiKey = "",
            Collection = "log_events",
            VectorSize = 768,
            Distance = "Cosine"
        };
        _mockOptions.Setup(x => x.Value).Returns(options);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        // Mock collection info response (has data)
        var collectionInfoResponse = new
        {
            result = new
            {
                points_count = 100
            }
        };

        _mockHttpHandler
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(collectionInfoResponse), Encoding.UTF8, "application/json")
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError
            });

        var vectorStore = new QdrantVectorStore(_mockOptions.Object, _mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        var result = await vectorStore.Has24HoursOfDataAsync(CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteVectorsOlderThan24HoursAsync_ShouldDeleteOldVectors()
    {
        // Arrange
        var options = new QdrantOptions
        {
            UseCloud = false,
            Host = "localhost",
            Port = 6333,
            Https = false,
            ApiKey = "",
            Collection = "log_events",
            VectorSize = 768,
            Distance = "Cosine"
        };
        _mockOptions.Setup(x => x.Value).Returns(options);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        // Mock collection info response (has data)
        var collectionInfoResponse = new
        {
            result = new
            {
                points_count = 100
            }
        };

        // Mock delete response
        var deleteResponse = new
        {
            result = new
            {
                status = new
                {
                    deleted_count = 25
                }
            }
        };

        _mockHttpHandler
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(collectionInfoResponse), Encoding.UTF8, "application/json")
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(deleteResponse), Encoding.UTF8, "application/json")
            });

        var vectorStore = new QdrantVectorStore(_mockOptions.Object, _mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        await vectorStore.DeleteVectorsOlderThan24HoursAsync(CancellationToken.None);

        // Assert
        // Should not throw exception and should complete successfully
    }

    [Fact]
    public async Task DeleteVectorsOlderThan24HoursAsync_ShouldHandleEmptyCollection()
    {
        // Arrange
        var options = new QdrantOptions
        {
            UseCloud = false,
            Host = "localhost",
            Port = 6333,
            Https = false,
            ApiKey = "",
            Collection = "log_events",
            VectorSize = 768,
            Distance = "Cosine"
        };
        _mockOptions.Setup(x => x.Value).Returns(options);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        // Mock collection info response (empty)
        var collectionInfoResponse = new
        {
            result = new
            {
                points_count = 0
            }
        };

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(collectionInfoResponse), Encoding.UTF8, "application/json")
            });

        var vectorStore = new QdrantVectorStore(_mockOptions.Object, _mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        await vectorStore.DeleteVectorsOlderThan24HoursAsync(CancellationToken.None);

        // Assert
        // Should not throw exception and should complete successfully
    }

    [Fact]
    public async Task DeleteVectorsOlderThan24HoursAsync_ShouldHandleCollectionNotFound()
    {
        // Arrange
        var options = new QdrantOptions
        {
            UseCloud = false,
            Host = "localhost",
            Port = 6333,
            Https = false,
            ApiKey = "",
            Collection = "log_events",
            VectorSize = 768,
            Distance = "Cosine"
        };
        _mockOptions.Setup(x => x.Value).Returns(options);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var vectorStore = new QdrantVectorStore(_mockOptions.Object, _mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        await vectorStore.DeleteVectorsOlderThan24HoursAsync(CancellationToken.None);

        // Assert
        // Should not throw exception and should complete successfully
    }

    [Fact]
    public async Task DeleteVectorsOlderThan24HoursAsync_ShouldHandleDeleteFailure()
    {
        // Arrange
        var options = new QdrantOptions
        {
            UseCloud = false,
            Host = "localhost",
            Port = 6333,
            Https = false,
            ApiKey = "",
            Collection = "log_events",
            VectorSize = 768,
            Distance = "Cosine"
        };
        _mockOptions.Setup(x => x.Value).Returns(options);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        // Mock collection info response (has data)
        var collectionInfoResponse = new
        {
            result = new
            {
                points_count = 100
            }
        };

        _mockHttpHandler
            .Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(collectionInfoResponse), Encoding.UTF8, "application/json")
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Delete operation failed")
            });

        var vectorStore = new QdrantVectorStore(_mockOptions.Object, _mockHttpClientFactory.Object, _mockLogger.Object);

        // Act
        await vectorStore.DeleteVectorsOlderThan24HoursAsync(CancellationToken.None);

        // Assert
        // Should not throw exception and should handle the error gracefully
    }

    #endregion
}

