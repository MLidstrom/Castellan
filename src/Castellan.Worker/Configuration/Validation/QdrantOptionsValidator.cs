using Microsoft.Extensions.Options;
using Castellan.Worker.VectorStores;

namespace Castellan.Worker.Configuration.Validation;

/// <summary>
/// Validates QdrantOptions configuration at startup
/// </summary>
public class QdrantOptionsValidator : IValidateOptions<QdrantOptions>
{
    public ValidateOptionsResult Validate(string? name, QdrantOptions options)
    {
        var failures = new List<string>();

        // Validate Host
        if (string.IsNullOrWhiteSpace(options.Host))
        {
            failures.Add("Qdrant Host is required");
        }

        // Validate Port
        if (options.Port <= 0 || options.Port > 65535)
        {
            failures.Add("Qdrant Port must be between 1 and 65535");
        }

        // Validate Collection Name
        if (string.IsNullOrWhiteSpace(options.Collection))
        {
            failures.Add("Qdrant Collection is required");
        }

        // If HTTPS is enabled, warn about potential certificate issues
        if (options.Https && string.IsNullOrEmpty(options.ApiKey))
        {
            // This is a warning, not a failure - local HTTPS might not need API key
            Console.WriteLine("⚠️  Warning: HTTPS is enabled but no API key is configured. Ensure Qdrant server certificates are properly configured.");
        }

        // Validate vector size
        if (options.VectorSize <= 0)
        {
            failures.Add("Qdrant VectorSize must be greater than 0");
        }

        if (failures.Count > 0)
        {
            return ValidateOptionsResult.Fail($"Qdrant configuration validation failed: {string.Join(", ", failures)}");
        }

        return ValidateOptionsResult.Success;
    }
}
