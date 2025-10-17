using System.Diagnostics;
using System.Text.Json;
using Castellan.Worker.Models.Chat;
using Castellan.Worker.Abstractions;

namespace Castellan.Worker.Services.Chat;

/// <summary>
/// Main chat service orchestrating intent classification, context retrieval, and response generation.
/// Implements RAG (Retrieval-Augmented Generation) for context-aware security analysis.
/// </summary>
public class ChatService : IChatService
{
    private readonly IIntentClassifier _intentClassifier;
    private readonly IContextRetriever _contextRetriever;
    private readonly IConversationManager _conversationManager;
    private readonly ILlmClient _llm;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IIntentClassifier intentClassifier,
        IContextRetriever contextRetriever,
        IConversationManager conversationManager,
        ILlmClient llm,
        ILogger<ChatService> logger)
    {
        _intentClassifier = intentClassifier;
        _contextRetriever = contextRetriever;
        _conversationManager = conversationManager;
        _llm = llm;
        _logger = logger;
    }

    public async Task<ChatResponse> ProcessMessageAsync(ChatRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var metrics = new PerformanceMetrics();

        try
        {
            _logger.LogInformation("Processing chat message: {Message}", request.Message);

            // Step 1: Get or create conversation
            var conversation = await GetOrCreateConversationAsync(request.ConversationId, request.UserId, ct);

            // SIMPLIFIED MODE: Skip intent classification for faster responses
            // Create a simple query intent
            var intent = new ChatIntent
            {
                Type = IntentType.Query,
                Confidence = 0.8f,
                RequiresAction = false,
                Entities = new Dictionary<string, string>()
            };
            metrics.IntentClassificationMs = 0;

            _logger.LogInformation("Using simplified mode - skipping intent classification");

            // Classify if this is a security-related question or casual conversation
            var isSecurityQuery = IsSecurityRelatedQuery(request.Message);

            ChatContext context;
            if (isSecurityQuery)
            {
                _logger.LogInformation("Security query detected - retrieving security context");

                // Step 3: Retrieve context (simplified)
                var contextSw = Stopwatch.StartNew();
                context = await _contextRetriever.RetrieveContextAsync(
                    request.Message,
                    intent,
                    request.ContextOptions ?? new ContextOptions
                    {
                        MaxSimilarEvents = 3,  // Reduced from 5
                        MaxRecentCriticalEvents = 5,  // Reduced from 10
                        IncludeCorrelationPatterns = false,  // Disabled for speed
                        IncludeSystemMetrics = true
                    },
                    ct);
                metrics.ContextRetrievalMs = contextSw.ElapsedMilliseconds;
                metrics.EventsRetrieved = context.EventCount;
            }
            else
            {
                _logger.LogInformation("Casual conversation detected - skipping security context");

                // Create empty context for non-security queries
                context = new ChatContext
                {
                    Intent = intent,
                    TimeRange = TimeRange.Last24Hours,
                    SimilarEvents = new List<Models.SecurityEvent>(),
                    RecentCriticalEvents = new List<Models.SecurityEvent>(),
                    ActivePatterns = new List<CorrelationPattern>()
                };
                metrics.ContextRetrievalMs = 0;
                metrics.EventsRetrieved = 0;
            }

            // Step 4: Generate response (simplified prompt)
            var llmSw = Stopwatch.StartNew();
            var assistantMessage = await GenerateSimplifiedResponseAsync(
                request.Message,
                context,
                conversation.Messages,
                ct);
            metrics.LlmGenerationMs = llmSw.ElapsedMilliseconds;

            // Step 5: Save messages to conversation
            // Use distinct timestamps to ensure correct ordering
            var baseTime = DateTime.UtcNow;
            var userMessage = new ChatMessage
            {
                ConversationId = conversation.Id,
                Role = MessageRole.User,
                Content = request.Message,
                Intent = intent,
                Timestamp = baseTime  // User message first
            };

            // Assistant message gets timestamp 1 second later to ensure ordering
            assistantMessage.Timestamp = baseTime.AddSeconds(1);

            // Save both messages in a single transaction to avoid nested transaction issues
            await _conversationManager.AddMessagesAsync(conversation.Id, new[] { userMessage, assistantMessage }, ct);

            // Step 6: Generate suggested follow-ups
            var followUps = await GenerateSuggestedFollowUpsAsync(conversation.Id, ct);

            sw.Stop();
            metrics.TotalMs = sw.ElapsedMilliseconds;

            _logger.LogInformation(
                "Chat message processed in {TotalMs}ms (intent: {IntentMs}ms, context: {ContextMs}ms, llm: {LlmMs}ms)",
                metrics.TotalMs,
                metrics.IntentClassificationMs,
                metrics.ContextRetrievalMs,
                metrics.LlmGenerationMs);

            return new ChatResponse
            {
                Message = assistantMessage,
                ConversationId = conversation.Id,
                ConversationTitle = conversation.Title,
                Intent = intent,
                Context = context,
                SuggestedFollowUps = followUps,
                IsComplete = true,
                Metrics = metrics
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process chat message: {Message}", request.Message);

            sw.Stop();
            metrics.TotalMs = sw.ElapsedMilliseconds;

            return new ChatResponse
            {
                Error = $"Failed to process message: {ex.Message}",
                IsComplete = false,
                Metrics = metrics
            };
        }
    }

    public async Task<List<string>> GenerateSuggestedFollowUpsAsync(string conversationId, CancellationToken ct = default)
    {
        try
        {
            var conversation = await _conversationManager.GetConversationAsync(conversationId, ct);
            if (conversation == null || conversation.MessageCount < 2)
            {
                return GetDefaultFollowUps();
            }

            var lastMessage = conversation.LastMessage;
            if (lastMessage == null || lastMessage.Role != MessageRole.Assistant)
            {
                return GetDefaultFollowUps();
            }

            // Generate contextual follow-ups based on last message
            var systemPrompt = @"You are a helpful assistant suggesting follow-up questions for a security monitoring conversation.
Generate 3 relevant follow-up questions based on the conversation context.
Respond ONLY with a JSON array of strings.

Example:
[""Show me more details about event X"", ""Are there any related incidents?"", ""What actions should I take?""]";

            var userPrompt = $@"Last assistant message: ""{lastMessage.Content}""

Generate 3 relevant follow-up questions (JSON array only):";

            var responseJson = await _llm.GenerateAsync(systemPrompt, userPrompt, ct);
            var followUps = JsonSerializer.Deserialize<List<string>>(responseJson);

            return followUps ?? GetDefaultFollowUps();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate suggested follow-ups for conversation {ConversationId}", conversationId);
            return GetDefaultFollowUps();
        }
    }

    private async Task<Conversation> GetOrCreateConversationAsync(string? conversationId, string userId, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(conversationId))
        {
            var existing = await _conversationManager.GetConversationAsync(conversationId, ct);
            if (existing != null)
            {
                return existing;
            }
        }

        // Create new conversation
        return await _conversationManager.CreateConversationAsync(userId, ct);
    }

    private async Task<ChatMessage> GenerateResponseAsync(
        string userMessage,
        ChatIntent intent,
        ChatContext context,
        List<ChatMessage> conversationHistory,
        ChatRequest request,
        CancellationToken ct)
    {
        var systemPrompt = BuildSystemPrompt(intent, context);
        var userPrompt = BuildUserPrompt(userMessage, conversationHistory);

        var responseText = await _llm.GenerateAsync(systemPrompt, userPrompt, ct);

        var message = new ChatMessage
        {
            Role = MessageRole.Assistant,
            Content = responseText,
            Timestamp = DateTime.UtcNow
        };

        // Add citations from context
        if (request.MaxCitations > 0)
        {
            message.Citations = GenerateCitations(context, request.MaxCitations);
        }

        // Add suggested actions
        if (request.IncludeSuggestedActions && intent.RequiresAction)
        {
            message.SuggestedActions = GenerateSuggestedActions(intent, context);
        }

        // Add visualizations
        if (request.IncludeVisualizations)
        {
            message.Visualizations = GenerateVisualizations(intent, context);
        }

        return message;
    }

    private string BuildSystemPrompt(ChatIntent intent, ChatContext context)
    {
        var prompt = @"You are an AI security analyst assistant for the Castellan security monitoring platform.
Your role is to help security analysts with:
- Threat hunting and investigation
- Security event analysis
- Compliance monitoring
- Incident response

Guidelines:
- Be concise and actionable
- Cite specific events when relevant
- Provide risk context and severity assessments
- Suggest next steps for investigations
- Use security terminology appropriately
- Always prioritize security and accuracy

Current Context:
";

        if (context.CurrentMetrics != null)
        {
            var m = context.CurrentMetrics;
            prompt += $@"
System Metrics (Last 24 hours):
- Total Events: {m.TotalEvents24h}
- Critical Events: {m.CriticalEvents}
- High Risk Events: {m.HighRiskEvents}
- Open Events: {m.OpenEvents}
";
        }

        if (context.SimilarEvents.Count > 0)
        {
            prompt += $"\nRelevant Security Events: {context.SimilarEvents.Count} events found\n";
            foreach (var evt in context.SimilarEvents.Take(3))
            {
                prompt += $"- [{evt.OriginalEvent.EventId}] {evt.EventType} - {evt.RiskLevel} risk - {evt.Summary}\n";
            }
        }

        if (context.RecentCriticalEvents.Count > 0)
        {
            prompt += $"\nRecent Critical Events: {context.RecentCriticalEvents.Count} events\n";
            foreach (var evt in context.RecentCriticalEvents.Take(3))
            {
                prompt += $"- [{evt.OriginalEvent.EventId}] {evt.EventType} at {evt.OriginalEvent.Time:HH:mm} - {evt.Summary}\n";
            }
        }

        if (context.ActivePatterns.Count > 0)
        {
            prompt += $"\nActive Correlation Patterns: {context.ActivePatterns.Count} patterns detected\n";
            foreach (var pattern in context.ActivePatterns.Take(3))
            {
                prompt += $"- {pattern.Name}: {pattern.EventCount} events (Score: {pattern.Score:P0})\n";
            }
        }

        prompt += $"\nUser Intent: {intent.Type} (Confidence: {intent.Confidence:P0})\n";

        return prompt;
    }

    private string BuildUserPrompt(string userMessage, List<ChatMessage> conversationHistory)
    {
        var prompt = "";

        // Include recent conversation history
        if (conversationHistory.Count > 0)
        {
            prompt += "Conversation History:\n";
            foreach (var msg in conversationHistory.TakeLast(5))
            {
                prompt += $"{msg.Role}: {msg.Content}\n";
            }
            prompt += "\n";
        }

        prompt += $"User: {userMessage}";

        return prompt;
    }

    private List<Citation> GenerateCitations(ChatContext context, int maxCitations)
    {
        var citations = new List<Citation>();

        // Add citations for similar events
        foreach (var evt in context.SimilarEvents.Take(maxCitations))
        {
            citations.Add(new Citation
            {
                Type = "event",
                SourceId = evt.Id,
                DisplayText = $"Event {evt.OriginalEvent.EventId}: {evt.Summary}",
                Url = $"/security-events/{evt.Id}",
                Relevance = 0.9f
            });

            if (citations.Count >= maxCitations) break;
        }

        // Add citations for critical events
        foreach (var evt in context.RecentCriticalEvents.Take(maxCitations - citations.Count))
        {
            citations.Add(new Citation
            {
                Type = "event",
                SourceId = evt.Id,
                DisplayText = $"Critical Event: {evt.EventType}",
                Url = $"/security-events/{evt.Id}",
                Relevance = 0.8f
            });

            if (citations.Count >= maxCitations) break;
        }

        return citations;
    }

    private List<SuggestedAction> GenerateSuggestedActions(ChatIntent intent, ChatContext context)
    {
        var actions = new List<SuggestedAction>();

        if (intent.Type == IntentType.Investigate && context.SimilarEvents.Count > 0)
        {
            actions.Add(new SuggestedAction
            {
                Type = "investigate",
                Label = "View Event Details",
                Description = "View full details of the most relevant event",
                Parameters = new Dictionary<string, object>
                {
                    ["eventId"] = context.SimilarEvents.First().Id
                },
                Icon = "search",
                Confidence = 0.9f
            });
        }

        if (context.RecentCriticalEvents.Count > 0)
        {
            actions.Add(new SuggestedAction
            {
                Type = "review",
                Label = "Review Critical Events",
                Description = "Review all critical events from the last 24 hours",
                Parameters = new Dictionary<string, object>
                {
                    ["timeRange"] = "24h",
                    ["riskLevel"] = "Critical"
                },
                Icon = "alert-triangle",
                Confidence = 0.8f
            });
        }

        return actions;
    }

    private List<Visualization> GenerateVisualizations(ChatIntent intent, ChatContext context)
    {
        var visualizations = new List<Visualization>();

        // Add risk distribution chart if we have metrics
        if (context.CurrentMetrics?.EventsByRiskLevel.Count > 0)
        {
            visualizations.Add(new Visualization
            {
                Type = "chart",
                Title = "Events by Risk Level (Last 24h)",
                Data = context.CurrentMetrics.EventsByRiskLevel,
                Config = new Dictionary<string, object>
                {
                    ["chartType"] = "pie",
                    ["colors"] = new[] { "#ef4444", "#f59e0b", "#eab308", "#22c55e" }
                }
            });
        }

        return visualizations;
    }

    private async Task<ChatMessage> GenerateSimplifiedResponseAsync(
        string userMessage,
        ChatContext context,
        List<ChatMessage> conversationHistory,
        CancellationToken ct)
    {
        // Check if this is a casual conversation (no security events in context)
        var isCasualConversation = context.SimilarEvents.Count == 0 && context.RecentCriticalEvents.Count == 0;

        string systemPrompt;
        if (isCasualConversation)
        {
            // Simple, friendly prompt for casual conversation
            systemPrompt = @"You are a helpful AI assistant for the Castellan security monitoring platform.
Be friendly, conversational, and helpful. Answer questions naturally.
If the user asks about security or wants to know about threats, you can offer to help them search for security events.";
        }
        else
        {
            // ENHANCED PROMPT: Detailed security analyst persona with strict anti-hallucination rules
            systemPrompt = @"You are an expert security analyst assistant for the Castellan security monitoring platform. Your role is to provide comprehensive, actionable security insights based ONLY on the provided data.

CRITICAL RULES - NEVER VIOLATE:
1. ONLY reference events, machines, and data that are explicitly provided in the context below
2. NEVER invent or fabricate Event IDs, hostnames, IP addresses, or security findings
3. If no relevant events are found, acknowledge this clearly: 'No critical events found in the provided data'
4. DO NOT greet with 'Good morning' - use neutral greetings like 'Hello' or skip greetings for follow-ups
5. If the system is healthy with no threats, say so directly - don't invent problems

Response Guidelines:
- For follow-up questions, respond directly and contextually based on conversation history
- Avoid repeating information already provided unless specifically asked
- Provide a clear security posture summary using bullet points
- Only highlight findings that appear in the provided context data
- Reference specific events with their ACTUAL Event IDs and machines from the context
- Suggest actionable next steps based on real data patterns
- Use professional security terminology
- Structure responses with clear sections (summary, findings, recommendations)
- Be proactive in identifying correlations ONLY from provided patterns";
        }

        var userPrompt = "";

        // Add conversation history for context continuity
        if (conversationHistory != null && conversationHistory.Count > 0)
        {
            userPrompt += "Conversation History:\n";
            // Include last 6 messages (3 exchanges) for context
            foreach (var msg in conversationHistory.TakeLast(6))
            {
                var role = msg.Role == MessageRole.User ? "User" : "Assistant";
                // Truncate long messages to keep prompt manageable
                var content = msg.Content.Length > 200 ? msg.Content.Substring(0, 197) + "..." : msg.Content;
                userPrompt += $"{role}: {content}\n";
            }
            userPrompt += "\n";
        }

        userPrompt += $"Current User Query: {userMessage}\n\n";

        // Skip security context for casual conversations
        if (isCasualConversation)
        {
            // No need to add security data for casual conversation
            var casualResponse = await _llm.GenerateAsync(systemPrompt, userPrompt, ct);

            return new ChatMessage
            {
                Role = MessageRole.Assistant,
                Content = casualResponse,
                Timestamp = DateTime.UtcNow,
                Citations = new List<Citation>(),
                SuggestedActions = new List<SuggestedAction>(),
                Visualizations = new List<Visualization>()
            };
        }

        // Add comprehensive context for security queries
        if (context.CurrentMetrics != null)
        {
            var m = context.CurrentMetrics;
            userPrompt += $@"Current Security Posture (Last 24 hours):
- Total Events: {m.TotalEvents24h}
- Critical Events: {m.CriticalEvents}
- High Risk Events: {m.HighRiskEvents}
- Open Events: {m.OpenEvents}
";
        }

        // Add similar/relevant events with details
        if (context.SimilarEvents.Count > 0)
        {
            userPrompt += $"\n=== RELEVANT SECURITY EVENTS (ONLY USE THESE - DO NOT FABRICATE) ===\n";
            userPrompt += $"Found: {context.SimilarEvents.Count} events\n\n";
            foreach (var evt in context.SimilarEvents.Take(5))
            {
                userPrompt += $"Windows Event ID: {evt.OriginalEvent.EventId}\n";
                userPrompt += $"Event Type: {evt.EventType}\n";
                userPrompt += $"Host: {evt.OriginalEvent.Host}\n";
                userPrompt += $"Risk Level: {evt.RiskLevel}\n";
                userPrompt += $"Time: {evt.OriginalEvent.Time:yyyy-MM-dd HH:mm:ss}\n";
                userPrompt += $"User: {evt.OriginalEvent.User}\n";
                userPrompt += $"Summary: {evt.Summary}\n";
                userPrompt += $"Database ID: {evt.Id}\n\n";
            }
        }
        else
        {
            userPrompt += $"\n=== RELEVANT SECURITY EVENTS ===\n";
            userPrompt += "No similar events found for this query.\n\n";
        }

        // Add recent critical events
        if (context.RecentCriticalEvents.Count > 0)
        {
            userPrompt += $"\n=== RECENT CRITICAL/HIGH-RISK EVENTS (ONLY USE THESE - DO NOT FABRICATE) ===\n";
            userPrompt += $"Found: {context.RecentCriticalEvents.Count} critical/high-risk events\n\n";
            foreach (var evt in context.RecentCriticalEvents.Take(5))
            {
                userPrompt += $"Windows Event ID: {evt.OriginalEvent.EventId}\n";
                userPrompt += $"Event Type: {evt.EventType}\n";
                userPrompt += $"Host: {evt.OriginalEvent.Host}\n";
                userPrompt += $"Risk Level: {evt.RiskLevel}\n";
                userPrompt += $"Time: {evt.OriginalEvent.Time:yyyy-MM-dd HH:mm:ss}\n";
                userPrompt += $"User: {evt.OriginalEvent.User}\n";
                userPrompt += $"Summary: {evt.Summary}\n";
                userPrompt += $"Database ID: {evt.Id}\n\n";
            }
        }
        else
        {
            userPrompt += $"\n=== RECENT CRITICAL/HIGH-RISK EVENTS ===\n";
            userPrompt += "No critical or high-risk events found in the last 24 hours.\n\n";
        }

        // Add correlation patterns if detected
        if (context.ActivePatterns.Count > 0)
        {
            userPrompt += $"\nActive Correlation Patterns Detected: {context.ActivePatterns.Count}\n";
            foreach (var pattern in context.ActivePatterns.Take(3))
            {
                userPrompt += $"- {pattern.Name}: {pattern.EventCount} events (Confidence: {pattern.Score:P0})\n";
            }
        }

        userPrompt += @"

=== INSTRUCTIONS ===
FIRST, determine if this is a security question or a conversational message:

**IF THIS IS A GREETING OR GENERAL CONVERSATION** (like 'hello', 'hi', 'how are you', 'thanks'):
- Respond naturally and conversationally
- DO NOT provide security analysis
- DO NOT mention security events, metrics, or system status
- Just be friendly and helpful

**IF THIS IS A SECURITY QUESTION**:
- Use ONLY the data provided above - NEVER fabricate Event IDs or findings
- If no relevant events were found, acknowledge the system is healthy
- Provide specific details with Event IDs, hosts, and timestamps
- Be concise and professional";

        var responseText = await _llm.GenerateAsync(systemPrompt, userPrompt, ct);

        // Generate citations from context
        var citations = GenerateCitations(context, 5);

        return new ChatMessage
        {
            Role = MessageRole.Assistant,
            Content = responseText,
            Timestamp = DateTime.UtcNow,
            Citations = citations,
            SuggestedActions = new List<SuggestedAction>(),
            Visualizations = new List<Visualization>()
        };
    }

    private List<string> GetDefaultFollowUps()
    {
        return new List<string>
        {
            "Show me critical events from the last hour",
            "Are there any active correlation patterns?",
            "What's the current system status?"
        };
    }

    private bool IsSecurityRelatedQuery(string message)
    {
        // Convert to lowercase for case-insensitive matching
        var lowerMessage = message.ToLower();

        // Common greetings and casual conversation patterns
        var casualPatterns = new[]
        {
            "hello", "hi", "hey", "greetings",
            "how are you", "how's it going", "what's up",
            "good morning", "good afternoon", "good evening",
            "thanks", "thank you", "bye", "goodbye",
            "ok", "okay", "sure", "yes", "no"
        };

        // If the message is very short and matches casual patterns, it's not security-related
        if (lowerMessage.Length < 30)
        {
            foreach (var pattern in casualPatterns)
            {
                if (lowerMessage.Contains(pattern))
                {
                    return false;
                }
            }
        }

        // Security-related keywords
        var securityKeywords = new[]
        {
            "event", "alert", "threat", "attack", "security",
            "critical", "risk", "vulnerability", "malware",
            "intrusion", "breach", "suspicious", "anomaly",
            "scan", "detection", "incident", "investigation",
            "firewall", "antivirus", "log", "audit",
            "credential", "authentication", "authorization",
            "exploit", "payload", "ransomware", "trojan",
            "status", "monitor", "dashboard", "report",
            "show", "find", "search", "list", "display"
        };

        // Check if message contains security-related keywords
        foreach (var keyword in securityKeywords)
        {
            if (lowerMessage.Contains(keyword))
            {
                return true;
            }
        }

        // Default to casual conversation if no security keywords found
        return false;
    }
}
