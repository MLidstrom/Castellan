using System.Diagnostics;
using System.Text.Json;
using Castellan.Worker.Models.Chat;
using Castellan.Worker.Abstractions;

namespace Castellan.Worker.Services.Chat;

/// <summary>
/// LLM-powered intent classifier for chat messages.
/// Analyzes user messages to determine intent type, extract entities, and suggest actions.
/// </summary>
public class IntentClassifier : IIntentClassifier
{
    private readonly ILlmClient _llm;
    private readonly ILogger<IntentClassifier> _logger;

    public IntentClassifier(
        ILlmClient llm,
        ILogger<IntentClassifier> logger)
    {
        _llm = llm;
        _logger = logger;
    }

    public async Task<ChatIntent> ClassifyIntentAsync(
        string message,
        List<ChatMessage> conversationHistory,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var systemPrompt = BuildSystemPrompt();
            var userPrompt = BuildUserPrompt(message, conversationHistory);

            _logger.LogDebug("Classifying intent for message: {Message}", message);

            var responseJson = await _llm.GenerateAsync(systemPrompt, userPrompt, ct);
            var intent = ParseIntentResponse(responseJson);

            sw.Stop();
            _logger.LogInformation(
                "Intent classified as {IntentType} with {Confidence:P0} confidence in {ElapsedMs}ms",
                intent.Type, intent.Confidence, sw.ElapsedMilliseconds);

            return intent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to classify intent for message: {Message}", message);

            // Return default conversational intent on failure
            return new ChatIntent
            {
                Type = IntentType.Conversational,
                Confidence = 0.5f,
                RequiresAction = false
            };
        }
    }

    private string BuildSystemPrompt()
    {
        return @"You are an intent classifier for a security monitoring system's AI assistant.
Analyze user messages and classify them into one of these intent types:

1. **Query** - General questions about security events or system status
   Examples: ""How many critical events today?"", ""What's my system status?""

2. **Investigate** - Investigation of specific events or incidents
   Examples: ""Show me details about event X"", ""What caused this alert?""

3. **Hunt** - Proactive threat hunting
   Examples: ""Are there any suspicious login patterns?"", ""Look for privilege escalation attempts""

4. **Compliance** - Compliance or regulatory queries
   Examples: ""Show me PCI-DSS violations"", ""Generate SOX compliance report""

5. **Explain** - Request for explanation of AI decisions
   Examples: ""Why was this classified as high risk?"", ""Explain this threat score""

6. **Action** - Action request (block IP, quarantine file, etc.)
   Examples: ""Block this IP address"", ""Quarantine this file""

7. **Conversational** - Conversational or unclear intent
   Examples: ""Hello"", ""Thanks"", ""I don't understand""

Extract relevant entities from the message:
- **eventId**: Security event IDs mentioned
- **ipAddress**: IP addresses mentioned
- **machineName**: Machine/host names mentioned
- **userName**: User account names mentioned
- **timeRange**: Time ranges mentioned (e.g., ""last hour"", ""today"", ""last 24 hours"")
- **riskLevel**: Risk levels mentioned (critical, high, medium, low)
- **eventType**: Event types mentioned (e.g., ""failed logons"", ""process creation"")

For Action intents, suggest the specific action to execute.

Respond ONLY with valid JSON in this format:
{
  ""intentType"": ""Query"",
  ""confidence"": 0.95,
  ""entities"": {
    ""timeRange"": ""today"",
    ""riskLevel"": ""critical""
  },
  ""requiresAction"": false,
  ""suggestedAction"": null,
  ""actionParameters"": {}
}";
    }

    private string BuildUserPrompt(string message, List<ChatMessage> conversationHistory)
    {
        var prompt = $"User message: \"{message}\"\n";

        // Include last 3 messages for context
        if (conversationHistory.Count > 0)
        {
            prompt += "\nConversation history (for context):\n";
            var recentMessages = conversationHistory.TakeLast(3);
            foreach (var msg in recentMessages)
            {
                prompt += $"- {msg.Role}: {msg.Content}\n";
            }
        }

        prompt += "\nClassify the intent and extract entities (respond with JSON only):";

        return prompt;
    }

    private ChatIntent ParseIntentResponse(string responseJson)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var response = JsonSerializer.Deserialize<IntentResponse>(responseJson, options);

            if (response == null)
            {
                throw new Exception("Failed to parse intent response");
            }

            return new ChatIntent
            {
                Type = ParseIntentType(response.IntentType),
                Confidence = response.Confidence,
                Entities = response.Entities ?? new Dictionary<string, string>(),
                RequiresAction = response.RequiresAction,
                SuggestedAction = response.SuggestedAction,
                ActionParameters = response.ActionParameters ?? new Dictionary<string, object>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse intent response: {Response}", responseJson);
            throw;
        }
    }

    private IntentType ParseIntentType(string intentType)
    {
        return intentType.ToLowerInvariant() switch
        {
            "query" => IntentType.Query,
            "investigate" => IntentType.Investigate,
            "hunt" => IntentType.Hunt,
            "compliance" => IntentType.Compliance,
            "explain" => IntentType.Explain,
            "action" => IntentType.Action,
            "conversational" => IntentType.Conversational,
            _ => IntentType.Conversational
        };
    }

    private class IntentResponse
    {
        public string IntentType { get; set; } = "";
        public float Confidence { get; set; }
        public Dictionary<string, string>? Entities { get; set; }
        public bool RequiresAction { get; set; }
        public string? SuggestedAction { get; set; }
        public Dictionary<string, object>? ActionParameters { get; set; }
    }
}
