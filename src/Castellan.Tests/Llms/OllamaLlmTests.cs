using System;
using System.Collections.Generic;
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
using Castellan.Worker.Llms;
using Castellan.Tests.TestUtilities;

namespace Castellan.Tests.Llms;

public class OllamaLlmTests
{
    private readonly Mock<IOptions<LlmOptions>> _mockOptions;
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;

    public OllamaLlmTests()
    {
        _mockOptions = new Mock<IOptions<LlmOptions>>();
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);
    }

    [Fact]
    public void Constructor_ShouldCreateInstance()
    {
        // Arrange
        var options = new LlmOptions
        {
            Provider = "Ollama",
            Endpoint = "http://localhost:11434",
            Model = "llama3.1:8b-instruct-q8_0",
            OpenAIModel = "gpt-4o-mini",
            OpenAIKey = ""
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        // Act
        var llm = new OllamaLlm(_mockOptions.Object, _httpClient);

        // Assert
        llm.Should().NotBeNull();
    }

    [Fact]
    public async Task AnalyzeAsync_WithValidEvent_ShouldReturnAnalysis()
    {
        // Arrange
        var options = new LlmOptions
        {
            Provider = "Ollama",
            Endpoint = "http://localhost:11434",
            Model = "llama3.1:8b-instruct-q8_0",
            OpenAIModel = "gpt-4o-mini",
            OpenAIKey = ""
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4624,
            "Information",
            "testuser",
            "An account was successfully logged on"
        );

        var similarEvents = new List<LogEvent>
        {
            new LogEvent(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                "TEST-HOST",
                "Security",
                4624,
                "Information",
                "testuser",
                "An account was successfully logged on"
            )
        };

        var expectedResponse = @"{
            ""risk"": ""low"",
            ""mitre"": [""T1078""],
            ""confidence"": 85,
            ""summary"": ""Successful login detected"",
            ""recommended_actions"": [""Monitor user activity""]
        }";

        var responseJson = JsonSerializer.Serialize(new { response = expectedResponse });

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

        var llm = new OllamaLlm(_mockOptions.Object, _httpClient);

        // Act
        var result = await llm.AnalyzeAsync(logEvent, similarEvents, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("risk");
        result.Should().Contain("mitre");
        result.Should().Contain("confidence");
        result.Should().Contain("summary");
        result.Should().Contain("recommended_actions");
    }

    [Fact]
    public async Task AnalyzeAsync_WithEmptySimilarEvents_ShouldReturnAnalysis()
    {
        // Arrange
        var options = new LlmOptions
        {
            Provider = "Ollama",
            Endpoint = "http://localhost:11434",
            Model = "llama3.1:8b-instruct-q8_0",
            OpenAIModel = "gpt-4o-mini",
            OpenAIKey = ""
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4625,
            "Warning",
            "testuser",
            "An account failed to log on"
        );

        var similarEvents = new List<LogEvent>();

        var expectedResponse = @"{
            ""risk"": ""medium"",
            ""mitre"": [""T1110""],
            ""confidence"": 75,
            ""summary"": ""Failed login attempt detected"",
            ""recommended_actions"": [""Investigate source""]
        }";

        var responseJson = JsonSerializer.Serialize(new { response = expectedResponse });

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

        var llm = new OllamaLlm(_mockOptions.Object, _httpClient);

        // Act
        var result = await llm.AnalyzeAsync(logEvent, similarEvents, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("risk");
        result.Should().Contain("mitre");
        result.Should().Contain("confidence");
        result.Should().Contain("summary");
        result.Should().Contain("recommended_actions");
    }

    [Fact]
    public async Task AnalyzeAsync_WithMultipleSimilarEvents_ShouldReturnAnalysis()
    {
        // Arrange
        var options = new LlmOptions
        {
            Provider = "Ollama",
            Endpoint = "http://localhost:11434",
            Model = "llama3.1:8b-instruct-q8_0",
            OpenAIModel = "gpt-4o-mini",
            OpenAIKey = ""
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4625,
            "Warning",
            "testuser",
            "An account failed to log on"
        );

        var similarEvents = new List<LogEvent>
        {
            new LogEvent(
                DateTimeOffset.UtcNow.AddMinutes(-1),
                "TEST-HOST",
                "Security",
                4625,
                "Warning",
                "testuser",
                "An account failed to log on"
            ),
            new LogEvent(
                DateTimeOffset.UtcNow.AddMinutes(-2),
                "TEST-HOST",
                "Security",
                4625,
                "Warning",
                "testuser",
                "An account failed to log on"
            ),
            new LogEvent(
                DateTimeOffset.UtcNow.AddMinutes(-3),
                "TEST-HOST",
                "Security",
                4625,
                "Warning",
                "testuser",
                "An account failed to log on"
            )
        };

        var expectedResponse = @"{
            ""risk"": ""high"",
            ""mitre"": [""T1110"", ""T1078""],
            ""confidence"": 90,
            ""summary"": ""Multiple failed login attempts detected"",
            ""recommended_actions"": [""Block IP address"", ""Reset password""]
        }";

        var responseJson = JsonSerializer.Serialize(new { response = expectedResponse });

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

        var llm = new OllamaLlm(_mockOptions.Object, _httpClient);

        // Act
        var result = await llm.AnalyzeAsync(logEvent, similarEvents, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("risk");
        result.Should().Contain("mitre");
        result.Should().Contain("confidence");
        result.Should().Contain("summary");
        result.Should().Contain("recommended_actions");
    }

    [Fact]
    public async Task AnalyzeAsync_WithHttpError_ShouldThrowException()
    {
        // Arrange
        var options = new LlmOptions
        {
            Provider = "Ollama",
            Endpoint = "http://localhost:11434",
            Model = "llama3.1:8b-instruct-q8_0",
            OpenAIModel = "gpt-4o-mini",
            OpenAIKey = ""
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4624,
            "Information",
            "testuser",
            "An account was successfully logged on"
        );

        var similarEvents = new List<LogEvent>();

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

        var llm = new OllamaLlm(_mockOptions.Object, _httpClient);

        // Act & Assert
        var action = () => llm.AnalyzeAsync(logEvent, similarEvents, CancellationToken.None);
        await action.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task AnalyzeAsync_WithInvalidJson_ShouldReturnEmptyString()
    {
        // Arrange
        var options = new LlmOptions
        {
            Provider = "Ollama",
            Endpoint = "http://localhost:11434",
            Model = "llama3.1:8b-instruct-q8_0",
            OpenAIModel = "gpt-4o-mini",
            OpenAIKey = ""
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4624,
            "Information",
            "testuser",
            "An account was successfully logged on"
        );

        var similarEvents = new List<LogEvent>();

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

        var llm = new OllamaLlm(_mockOptions.Object, _httpClient);

        // Act
        var result = await llm.AnalyzeAsync(logEvent, similarEvents, CancellationToken.None);

        // Assert
        result.Should().Be("");
    }

    [Fact]
    public async Task AnalyzeAsync_WithMissingResponseField_ShouldReturnEmptyString()
    {
        // Arrange
        var options = new LlmOptions
        {
            Provider = "Ollama",
            Endpoint = "http://localhost:11434",
            Model = "llama3.1:8b-instruct-q8_0",
            OpenAIModel = "gpt-4o-mini",
            OpenAIKey = ""
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4624,
            "Information",
            "testuser",
            "An account was successfully logged on"
        );

        var similarEvents = new List<LogEvent>();

        var responseJson = JsonSerializer.Serialize(new { other_field = "value" });

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

        var llm = new OllamaLlm(_mockOptions.Object, _httpClient);

        // Act
        var result = await llm.AnalyzeAsync(logEvent, similarEvents, CancellationToken.None);

        // Assert
        result.Should().Be("");
    }

    [Fact]
    public async Task AnalyzeAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var options = new LlmOptions
        {
            Provider = "Ollama",
            Endpoint = "http://localhost:11434",
            Model = "llama3.1:8b-instruct-q8_0",
            OpenAIModel = "gpt-4o-mini",
            OpenAIKey = ""
        };
        _mockOptions.Setup(x => x.Value).Returns(options);

        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            "TEST-HOST",
            "Security",
            4624,
            "Information",
            "testuser",
            "An account was successfully logged on"
        );

        var similarEvents = new List<LogEvent>();

        var llm = new OllamaLlm(_mockOptions.Object, _httpClient);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        var action = () => llm.AnalyzeAsync(logEvent, similarEvents, cts.Token);
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void LlmOptions_ShouldHaveDefaultValues()
    {
        // Act
        var options = new LlmOptions();

        // Assert
        options.Provider.Should().Be("Ollama");
        options.Endpoint.Should().Be("http://localhost:11434");
        options.Model.Should().Be("llama3.1:8b-instruct-q8_0");
        options.OpenAIModel.Should().Be("gpt-4o-mini");
        options.OpenAIKey.Should().Be("");
    }

    [Fact]
    public void LlmOptions_ShouldAllowCustomValues()
    {
        // Arrange
        var customProvider = "CustomProvider";
        var customEndpoint = "http://custom-endpoint:8080";
        var customModel = "custom-model";
        var customOpenAIModel = "custom-openai-model";
        var customOpenAIKey = "sk-custom-key";

        // Act
        var options = new LlmOptions
        {
            Provider = customProvider,
            Endpoint = customEndpoint,
            Model = customModel,
            OpenAIModel = customOpenAIModel,
            OpenAIKey = customOpenAIKey
        };

        // Assert
        options.Provider.Should().Be(customProvider);
        options.Endpoint.Should().Be(customEndpoint);
        options.Model.Should().Be(customModel);
        options.OpenAIModel.Should().Be(customOpenAIModel);
        options.OpenAIKey.Should().Be(customOpenAIKey);
    }

    [Fact]
    public void LlmOptions_ShouldHandleNullValues()
    {
        // Act
        var options = new LlmOptions
        {
            Provider = null!,
            Endpoint = null!,
            Model = null!,
            OpenAIModel = null!,
            OpenAIKey = null!
        };

        // Assert
        options.Provider.Should().BeNull();
        options.Endpoint.Should().BeNull();
        options.Model.Should().BeNull();
        options.OpenAIModel.Should().BeNull();
        options.OpenAIKey.Should().BeNull();
    }

    [Fact]
    public void LlmOptions_ShouldHandleEmptyStrings()
    {
        // Act
        var options = new LlmOptions
        {
            Provider = "",
            Endpoint = "",
            Model = "",
            OpenAIModel = "",
            OpenAIKey = ""
        };

        // Assert
        options.Provider.Should().Be("");
        options.Endpoint.Should().Be("");
        options.Model.Should().Be("");
        options.OpenAIModel.Should().Be("");
        options.OpenAIKey.Should().Be("");
    }

    [Fact]
    public void LlmOptions_ShouldBeMutable()
    {
        // Arrange
        var options = new LlmOptions();

        // Act
        options.Provider = "UpdatedProvider";
        options.Endpoint = "http://updated-endpoint:9090";
        options.Model = "updated-model";
        options.OpenAIModel = "updated-openai-model";
        options.OpenAIKey = "sk-updated-key";

        // Assert
        options.Provider.Should().Be("UpdatedProvider");
        options.Endpoint.Should().Be("http://updated-endpoint:9090");
        options.Model.Should().Be("updated-model");
        options.OpenAIModel.Should().Be("updated-openai-model");
        options.OpenAIKey.Should().Be("sk-updated-key");
    }

    [Fact]
    public void LlmOptions_ShouldHandleSpecialCharactersInEndpoint()
    {
        // Arrange
        var specialEndpoint = "https://api.example.com/v1/chat/completions?key=123&model=test";

        // Act
        var options = new LlmOptions
        {
            Endpoint = specialEndpoint
        };

        // Assert
        options.Endpoint.Should().Be(specialEndpoint);
    }

    [Fact]
    public void LlmOptions_ShouldHandleLongValues()
    {
        // Arrange
        var longProvider = new string('A', 1000);
        var longEndpoint = new string('B', 1000);
        var longModel = new string('C', 1000);
        var longOpenAIModel = new string('D', 1000);
        var longOpenAIKey = new string('E', 1000);

        // Act
        var options = new LlmOptions
        {
            Provider = longProvider,
            Endpoint = longEndpoint,
            Model = longModel,
            OpenAIModel = longOpenAIModel,
            OpenAIKey = longOpenAIKey
        };

        // Assert
        options.Provider.Should().Be(longProvider);
        options.Endpoint.Should().Be(longEndpoint);
        options.Model.Should().Be(longModel);
        options.OpenAIModel.Should().Be(longOpenAIModel);
        options.OpenAIKey.Should().Be(longOpenAIKey);
        options.Provider.Length.Should().Be(1000);
        options.Endpoint.Length.Should().Be(1000);
        options.Model.Length.Should().Be(1000);
        options.OpenAIModel.Length.Should().Be(1000);
        options.OpenAIKey.Length.Should().Be(1000);
    }
}

