using Castellan.Worker.Abstractions;
using Microsoft.Extensions.Options;

namespace Castellan.Worker.Embeddings;

public sealed class MockEmbedder : IEmbedder
{
    private readonly int _vectorSize;

    public MockEmbedder(IOptions<EmbeddingOptions> opt)
    {
        _vectorSize = 768; // Default vector size for testing
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        // Generate a consistent embedding based on the text hash
        var hash = text.GetHashCode();
        var random = new Random(hash); // Create new random with text hash for consistency
        
        var embedding = new float[_vectorSize];
        for (int i = 0; i < _vectorSize; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1); // Values between -1 and 1
        }
        
        // Normalize the vector to unit length
        var magnitude = Math.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < _vectorSize; i++)
        {
            embedding[i] = (float)(embedding[i] / magnitude);
        }
        
        return Task.FromResult(embedding);
    }
}

