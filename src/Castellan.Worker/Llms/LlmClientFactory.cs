using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Castellan.Worker.Abstractions;
using Castellan.Worker.Options;

namespace Castellan.Worker.Llms;

/// <summary>
/// Factory for creating ILlmClient instances with specific model configurations.
/// Creates fully decorated clients with resilience, strict JSON validation, and telemetry.
/// </summary>
public sealed class LlmClientFactory : ILlmClientFactory
{
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IOptions<ResilienceOptions> _resilienceOptions;
    private readonly IOptions<StrictJsonOptions> _strictJsonOptions;
    private readonly IOptions<OpenTelemetryOptions> _telemetryOptions;

    public LlmClientFactory(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        IOptions<ResilienceOptions> resilienceOptions,
        IOptions<StrictJsonOptions> strictJsonOptions,
        IOptions<OpenTelemetryOptions> telemetryOptions)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _resilienceOptions = resilienceOptions ?? throw new ArgumentNullException(nameof(resilienceOptions));
        _strictJsonOptions = strictJsonOptions ?? throw new ArgumentNullException(nameof(strictJsonOptions));
        _telemetryOptions = telemetryOptions ?? throw new ArgumentNullException(nameof(telemetryOptions));
    }

    /// <summary>
    /// Creates a new ILlmClient instance configured for the specified model.
    /// Applies full decorator chain: Base → Resilience → StrictJson → Telemetry
    /// </summary>
    public ILlmClient CreateClient(string modelName, string provider = "Ollama")
    {
        if (string.IsNullOrWhiteSpace(modelName))
            throw new ArgumentException("Model name cannot be null or empty", nameof(modelName));

        // Create base LLM client with specific model
        ILlmClient llm = CreateBaseLlmClient(modelName, provider);

        // Layer 1: Add resilience (if enabled)
        var llmResilienceEnabled = _configuration.GetValue<bool>("Resilience:LLM:Enabled", true);
        if (llmResilienceEnabled)
        {
            var resilienceLogger = _loggerFactory.CreateLogger<ResilientLlmClient>();
            llm = new ResilientLlmClient(llm, _resilienceOptions, resilienceLogger);
        }

        // Layer 2: Add strict JSON validation (if enabled)
        var strictJsonEnabled = _configuration.GetValue<bool>("StrictJson:Enabled", true);
        if (strictJsonEnabled)
        {
            var strictJsonLogger = _loggerFactory.CreateLogger<StrictJsonLlmClient>();
            llm = new StrictJsonLlmClient(llm, _strictJsonOptions, strictJsonLogger);
        }

        // Layer 3: Add telemetry (if enabled)
        var telemetryEnabled = _configuration.GetValue<bool>("OpenTelemetry:Enabled", true);
        if (telemetryEnabled)
        {
            var telemetryLogger = _loggerFactory.CreateLogger<TelemetryLlmClient>();
            llm = new TelemetryLlmClient(llm, _telemetryOptions, telemetryLogger);
        }

        return llm;
    }

    /// <summary>
    /// Creates the base LLM client (OllamaLlm or OpenAILlm) with specific model configuration.
    /// </summary>
    private ILlmClient CreateBaseLlmClient(string modelName, string provider)
    {
        // Create HttpClient with timeout for LLM requests
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(90) // 90 second timeout for LLM responses
        };

        if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            // Create OpenAI client with specific model
            var openAiOptions = new LlmOptions
            {
                Provider = "OpenAI",
                Model = modelName,
                Endpoint = _configuration["LLM:Endpoint"] ?? "https://api.openai.com/v1",
                OpenAIKey = _configuration["LLM:OpenAIKey"] ?? _configuration["OPENAI_API_KEY"] ?? "",
                OpenAIModel = modelName
            };

            return new OpenAILlm(Microsoft.Extensions.Options.Options.Create(openAiOptions), httpClient);
        }
        else if (provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
        {
            // Create Ollama client with specific model
            var ollamaOptions = new LlmOptions
            {
                Provider = "Ollama",
                Model = modelName,
                Endpoint = _configuration["LLM:Endpoint"] ?? "http://localhost:11434"
            };

            return new OllamaLlm(Microsoft.Extensions.Options.Options.Create(ollamaOptions), httpClient);
        }
        else
        {
            throw new NotSupportedException($"Provider '{provider}' is not supported. Use 'Ollama' or 'OpenAI'.");
        }
    }
}
