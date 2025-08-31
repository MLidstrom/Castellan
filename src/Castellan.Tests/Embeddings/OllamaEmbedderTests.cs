using System;
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
using Castellan.Worker.Embeddings;

namespace Castellan.Tests.Embeddings;

public class OllamaEmbedderTests
{
    private readonly Mock<IOptions<EmbeddingOptions>> _mockOptions;
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;

    public OllamaEmbedderTests()
    {
        _mockOptions = new Mock<IOptions<EmbeddingOptions>>();
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);
    }

    [Fact]
    public void Constructor_ShouldCreateInstance()
    {
        // Arrange
        var options = new EmbeddingOptions
        {
            Provider = "Ollama",
            Endpoint = "http://localhost:11434",
            Model = "nomic-embed-text",
            OpenAIKey = ""
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        // Act
        var embedder = new OllamaEmbedder(_mockOptions.Object, _httpClient);

        // Assert
        embedder.Should().NotBeNull();
    }

    [Fact]
    public async Task EmbedAsync_WithValidText_ShouldReturnEmbedding()
    {
        // Arrange
        var options = new EmbeddingOptions
        {
            Provider = "Ollama",
            Endpoint = "http://localhost:11434",
            Model = "nomic-embed-text",
            OpenAIKey = ""
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        var expectedEmbedding = new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };
        var responseJson = JsonSerializer.Serialize(new { embedding = expectedEmbedding });

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

        var embedder = new OllamaEmbedder(_mockOptions.Object, _httpClient);

        // Act
        var result = await embedder.EmbedAsync("test text", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(5);
        result.Should().BeEquivalentTo(expectedEmbedding);
    }

    [Fact]
    public async Task EmbedAsync_WithEmptyText_ShouldReturnEmbedding()
    {
        // Arrange
        var options = new EmbeddingOptions
        {
            Provider = "Ollama",
            Endpoint = "http://localhost:11434",
            Model = "nomic-embed-text",
            OpenAIKey = ""
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        var expectedEmbedding = new float[] { 0.0f, 0.0f, 0.0f };
        var responseJson = JsonSerializer.Serialize(new { embedding = expectedEmbedding });

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

        var embedder = new OllamaEmbedder(_mockOptions.Object, _httpClient);

        // Act
        var result = await embedder.EmbedAsync("", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().BeEquivalentTo(expectedEmbedding);
    }

    [Fact]
    public async Task EmbedAsync_WithLongText_ShouldReturnEmbedding()
    {
        // Arrange
        var options = new EmbeddingOptions
        {
            Provider = "Ollama",
            Endpoint = "http://localhost:11434",
            Model = "nomic-embed-text",
            OpenAIKey = ""
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        var longText = new string('A', 10000);
        var expectedEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var responseJson = JsonSerializer.Serialize(new { embedding = expectedEmbedding });

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

        var embedder = new OllamaEmbedder(_mockOptions.Object, _httpClient);

        // Act
        var result = await embedder.EmbedAsync(longText, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().BeEquivalentTo(expectedEmbedding);
    }

    [Fact]
    public async Task EmbedAsync_WithSpecialCharacters_ShouldReturnEmbedding()
    {
        // Arrange
        var options = new EmbeddingOptions
        {
            Provider = "Ollama",
            Endpoint = "http://localhost:11434",
            Model = "nomic-embed-text",
            OpenAIKey = ""
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        var specialText = "Test text with special chars: !@#$%^&*()_+-=[]{}|;':\",./<>?";
        var expectedEmbedding = new float[] { 0.1f, 0.2f, 0.3f };
        var responseJson = JsonSerializer.Serialize(new { embedding = expectedEmbedding });

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

        var embedder = new OllamaEmbedder(_mockOptions.Object, _httpClient);

        // Act
        var result = await embedder.EmbedAsync(specialText, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().BeEquivalentTo(expectedEmbedding);
    }

    [Fact]
    public async Task EmbedAsync_WithHttpError_ShouldThrowException()
    {
        // Arrange
        var options = new EmbeddingOptions
        {
            Provider = "Ollama",
            Endpoint = "http://localhost:11434",
            Model = "nomic-embed-text",
            OpenAIKey = ""
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

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

        var embedder = new OllamaEmbedder(_mockOptions.Object, _httpClient);

        // Act & Assert
        var action = () => embedder.EmbedAsync("test text", CancellationToken.None);
        await action.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task EmbedAsync_WithInvalidJson_ShouldReturnEmptyArray()
    {
        // Arrange
        var options = new EmbeddingOptions
        {
            Provider = "Ollama",
            Endpoint = "http://localhost:11434",
            Model = "nomic-embed-text",
            OpenAIKey = ""
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        var invalidJson = "{ invalid json }";

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
                Content = new StringContent(invalidJson)
            });

        var embedder = new OllamaEmbedder(_mockOptions.Object, _httpClient);

        // Act
        var result = await embedder.EmbedAsync("test text", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task EmbedAsync_WithEmptyEmbeddingArray_ShouldReturnEmptyArray()
    {
        // Arrange
        var options = new EmbeddingOptions
        {
            Provider = "Ollama",
            Endpoint = "http://localhost:11434",
            Model = "nomic-embed-text",
            OpenAIKey = ""
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        var responseJson = JsonSerializer.Serialize(new { embedding = new float[0] });

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

        var embedder = new OllamaEmbedder(_mockOptions.Object, _httpClient);

        // Act
        var result = await embedder.EmbedAsync("test text", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task EmbedAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var options = new EmbeddingOptions
        {
            Provider = "Ollama",
            Endpoint = "http://localhost:11434",
            Model = "nomic-embed-text",
            OpenAIKey = ""
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        var embedder = new OllamaEmbedder(_mockOptions.Object, _httpClient);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var action = () => embedder.EmbedAsync("test text", cts.Token);
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void EmbeddingOptions_ShouldHaveDefaultValues()
    {
        // Act
        var options = new EmbeddingOptions();

        // Assert
        options.Provider.Should().Be("Ollama");
        options.Endpoint.Should().Be("http://localhost:11434");
        options.Model.Should().Be("nomic-embed-text");
        options.OpenAIKey.Should().Be("");
    }

    [Fact]
    public void EmbeddingOptions_ShouldAllowCustomValues()
    {
        // Arrange
        var customProvider = "CustomProvider";
        var customEndpoint = "http://custom-endpoint:8080";
        var customModel = "custom-model";
        var customOpenAIKey = "sk-custom-key";

        // Act
        var options = new EmbeddingOptions
        {
            Provider = customProvider,
            Endpoint = customEndpoint,
            Model = customModel,
            OpenAIKey = customOpenAIKey
        };

        // Assert
        options.Provider.Should().Be(customProvider);
        options.Endpoint.Should().Be(customEndpoint);
        options.Model.Should().Be(customModel);
        options.OpenAIKey.Should().Be(customOpenAIKey);
    }

    [Fact]
    public void EmbeddingOptions_ShouldHandleNullValues()
    {
        // Act
        var options = new EmbeddingOptions
        {
            Provider = null!,
            Endpoint = null!,
            Model = null!,
            OpenAIKey = null!
        };

        // Assert
        options.Provider.Should().BeNull();
        options.Endpoint.Should().BeNull();
        options.Model.Should().BeNull();
        options.OpenAIKey.Should().BeNull();
    }

    [Fact]
    public void EmbeddingOptions_ShouldHandleEmptyStrings()
    {
        // Act
        var options = new EmbeddingOptions
        {
            Provider = "",
            Endpoint = "",
            Model = "",
            OpenAIKey = ""
        };

        // Assert
        options.Provider.Should().Be("");
        options.Endpoint.Should().Be("");
        options.Model.Should().Be("");
        options.OpenAIKey.Should().Be("");
    }

    [Fact]
    public void EmbeddingOptions_ShouldBeMutable()
    {
        // Arrange
        var options = new EmbeddingOptions();

        // Act
        options.Provider = "UpdatedProvider";
        options.Endpoint = "http://updated-endpoint:9090";
        options.Model = "updated-model";
        options.OpenAIKey = "sk-updated-key";

        // Assert
        options.Provider.Should().Be("UpdatedProvider");
        options.Endpoint.Should().Be("http://updated-endpoint:9090");
        options.Model.Should().Be("updated-model");
        options.OpenAIKey.Should().Be("sk-updated-key");
    }

    [Fact]
    public void EmbeddingOptions_ShouldHandleSpecialCharactersInEndpoint()
    {
        // Arrange
        var specialEndpoint = "https://api.example.com/v1/embeddings?key=123&model=test";

        // Act
        var options = new EmbeddingOptions
        {
            Endpoint = specialEndpoint
        };

        // Assert
        options.Endpoint.Should().Be(specialEndpoint);
    }

    [Fact]
    public void EmbeddingOptions_ShouldHandleLongValues()
    {
        // Arrange
        var longProvider = new string('A', 1000);
        var longEndpoint = new string('B', 1000);
        var longModel = new string('C', 1000);
        var longOpenAIKey = new string('D', 1000);

        // Act
        var options = new EmbeddingOptions
        {
            Provider = longProvider,
            Endpoint = longEndpoint,
            Model = longModel,
            OpenAIKey = longOpenAIKey
        };

        // Assert
        options.Provider.Should().Be(longProvider);
        options.Endpoint.Should().Be(longEndpoint);
        options.Model.Should().Be(longModel);
        options.OpenAIKey.Should().Be(longOpenAIKey);
        options.Provider.Length.Should().Be(1000);
        options.Endpoint.Length.Should().Be(1000);
        options.Model.Length.Should().Be(1000);
        options.OpenAIKey.Length.Should().Be(1000);
    }
}

