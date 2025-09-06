using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Castellan.Worker.Configuration;

namespace Castellan.Worker.Services
{
    /// <summary>
    /// Text similarity hashing service for Phase 2B embedding cache implementation.
    /// Provides semantic text hashing with normalization and collision handling.
    /// </summary>
    public class TextHashingService
    {
        private readonly ILogger<TextHashingService> _logger;
        private readonly EmbeddingCacheOptions _options;

        // Regular expressions for text normalization (compiled for performance)
        private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex NonAlphanumericRegex = new(@"[^\w\s]", RegexOptions.Compiled);
        private static readonly Regex EventIdRegex = new(@"\b\d{4,}\b", RegexOptions.Compiled); // Event IDs
        private static readonly Regex TimestampRegex = new(@"\b\d{4}-\d{2}-\d{2}[T\s]\d{2}:\d{2}:\d{2}", RegexOptions.Compiled);
        private static readonly Regex IpAddressRegex = new(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", RegexOptions.Compiled);
        private static readonly Regex GuidRegex = new(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b", RegexOptions.Compiled);

        public TextHashingService(EmbeddingCacheOptions options, ILogger<TextHashingService> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Generates a semantic hash for the given text that focuses on content similarity.
        /// This hash is designed to produce similar values for semantically similar text.
        /// </summary>
        /// <param name="text">The text to hash</param>
        /// <returns>A hash string suitable for cache keys</returns>
        public string GenerateSemanticHash(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text cannot be null or empty", nameof(text));

            try
            {
                // Step 1: Apply text normalization if enabled
                var normalizedText = _options.EnableTextNormalization ? NormalizeText(text) : text;

                // Step 2: Truncate if too long
                if (normalizedText.Length > _options.MaxTextLength)
                {
                    normalizedText = normalizedText.Substring(0, _options.MaxTextLength);
                    _logger.LogDebug("Text truncated to {MaxLength} characters for hashing", _options.MaxTextLength);
                }

                // Step 3: Generate hash
                var hash = ComputeSemanticHash(normalizedText);

                _logger.LogDebug("Generated semantic hash for text (length: {Length}): {Hash}", 
                    text.Length, hash);

                return hash;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate semantic hash for text");
                // Fallback to simple hash
                return ComputeSimpleHash(text);
            }
        }

        /// <summary>
        /// Generates a fast hash for exact text matching (non-semantic).
        /// Used as a fallback or for exact content caching.
        /// </summary>
        /// <param name="text">The text to hash</param>
        /// <returns>A hash string for exact matching</returns>
        public string GenerateExactHash(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text cannot be null or empty", nameof(text));

            return ComputeSimpleHash(text);
        }

        /// <summary>
        /// Calculates the similarity score between two text strings.
        /// Used to determine if cached embeddings can be reused.
        /// </summary>
        /// <param name="text1">First text</param>
        /// <param name="text2">Second text</param>
        /// <returns>Similarity score between 0.0 and 1.0</returns>
        public double CalculateSimilarity(string text1, string text2)
        {
            if (string.IsNullOrWhiteSpace(text1) || string.IsNullOrWhiteSpace(text2))
                return 0.0;

            if (text1.Equals(text2, StringComparison.OrdinalIgnoreCase))
                return 1.0;

            // Normalize both texts
            var norm1 = _options.EnableTextNormalization ? NormalizeText(text1) : text1.ToLowerInvariant();
            var norm2 = _options.EnableTextNormalization ? NormalizeText(text2) : text2.ToLowerInvariant();

            if (norm1.Equals(norm2, StringComparison.Ordinal))
                return 1.0;

            // Calculate Jaccard similarity using word sets
            var similarity = CalculateJaccardSimilarity(norm1, norm2);
            
            _logger.LogDebug("Calculated text similarity: {Similarity:F3} for texts (lengths: {Len1}, {Len2})",
                similarity, text1.Length, text2.Length);

            return similarity;
        }

        /// <summary>
        /// Determines if two texts are similar enough for cache hit based on configured threshold.
        /// </summary>
        /// <param name="text1">First text</param>
        /// <param name="text2">Second text</param>
        /// <returns>True if texts are similar enough for cache reuse</returns>
        public bool AreSimilar(string text1, string text2)
        {
            var similarity = CalculateSimilarity(text1, text2);
            return similarity >= _options.SimilarityThreshold;
        }

        /// <summary>
        /// Generates a cache key combining semantic and structural information.
        /// </summary>
        /// <param name="text">The text to generate a cache key for</param>
        /// <param name="additionalContext">Optional additional context (e.g., event type, channel)</param>
        /// <returns>A cache key string</returns>
        public string GenerateCacheKey(string text, string? additionalContext = null)
        {
            var semanticHash = GenerateSemanticHash(text);
            
            if (string.IsNullOrEmpty(additionalContext))
            {
                return $"emb:{semanticHash}";
            }

            var contextHash = ComputeSimpleHash(additionalContext);
            return $"emb:{semanticHash}:{contextHash[..8]}"; // Use first 8 chars of context hash
        }

        private string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var normalized = text;

            // Step 1: Convert to lowercase
            normalized = normalized.ToLowerInvariant();

            // Step 2: Remove or normalize dynamic content that doesn't affect meaning
            normalized = TimestampRegex.Replace(normalized, "[TIMESTAMP]");
            normalized = IpAddressRegex.Replace(normalized, "[IPADDR]");
            normalized = GuidRegex.Replace(normalized, "[GUID]");
            normalized = EventIdRegex.Replace(normalized, "[EVENTID]");

            // Step 3: Remove punctuation and special characters (but keep spaces)
            normalized = NonAlphanumericRegex.Replace(normalized, " ");

            // Step 4: Normalize whitespace
            normalized = WhitespaceRegex.Replace(normalized, " ").Trim();

            return normalized;
        }

        private string ComputeSemanticHash(string normalizedText)
        {
            // Use a combination of content-based and structure-based hashing
            var contentHash = ComputeContentHash(normalizedText);
            var structureHash = ComputeStructureHash(normalizedText);
            
            // Combine the hashes
            var combinedInput = $"{contentHash}:{structureHash}";
            return ComputeSimpleHash(combinedInput)[..16]; // Use first 16 characters
        }

        private string ComputeContentHash(string text)
        {
            // Hash based on word frequency and order (simplified n-gram approach)
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            if (words.Length == 0)
                return "empty";

            // Create a signature based on most frequent words and their positions
            var wordFreq = new Dictionary<string, int>();
            foreach (var word in words)
            {
                if (word.Length > 2) // Skip very short words
                {
                    wordFreq[word] = wordFreq.GetValueOrDefault(word, 0) + 1;
                }
            }

            // Get top words by frequency
            var topWords = wordFreq
                .OrderByDescending(kvp => kvp.Value)
                .ThenBy(kvp => kvp.Key) // Consistent ordering
                .Take(10)
                .Select(kvp => kvp.Key);

            var signature = string.Join("|", topWords);
            return ComputeSimpleHash(signature);
        }

        private string ComputeStructureHash(string text)
        {
            // Hash based on text structure (length, word count, character distribution)
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var avgWordLength = words.Length > 0 ? words.Average(w => w.Length) : 0;
            var wordCount = words.Length;
            var charCount = text.Length;

            // Create a structural signature
            var structureSignature = $"wc:{wordCount:D4}|cc:{charCount:D6}|awl:{avgWordLength:F1}";
            
            return ComputeSimpleHash(structureSignature);
        }

        private string ComputeSimpleHash(string input)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hashBytes);
        }

        private double CalculateJaccardSimilarity(string text1, string text2)
        {
            var words1 = new HashSet<string>(text1.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            var words2 = new HashSet<string>(text2.Split(' ', StringSplitOptions.RemoveEmptyEntries));

            if (words1.Count == 0 && words2.Count == 0)
                return 1.0;

            if (words1.Count == 0 || words2.Count == 0)
                return 0.0;

            var intersection = words1.Intersect(words2).Count();
            var union = words1.Union(words2).Count();

            return (double)intersection / union;
        }
    }
}
