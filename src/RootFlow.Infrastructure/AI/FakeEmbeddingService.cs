using System.Security.Cryptography;
using System.Text.RegularExpressions;
using RootFlow.Application.Abstractions.AI;

namespace RootFlow.Infrastructure.AI;

public sealed class FakeEmbeddingService : IEmbeddingService
{
    private const int Dimensions = 1536;
    private static readonly Regex TokenRegex = new("[a-z0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CreateEmbedding(text));
    }

    public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default)
    {
        var embeddings = inputs.Select(CreateEmbedding).ToArray();
        return Task.FromResult<IReadOnlyList<float[]>>(embeddings);
    }

    private static float[] CreateEmbedding(string text)
    {
        var embedding = new float[Dimensions];
        var normalizedText = text.Trim().ToLowerInvariant();
        var matches = TokenRegex.Matches(normalizedText);

        if (matches.Count == 0)
        {
            embedding[0] = 1f;
            return embedding;
        }

        foreach (Match match in matches)
        {
            var token = match.Value;
            var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
            var bucket = Math.Abs(BitConverter.ToInt32(hash, 0)) % Dimensions;
            var sign = (hash[4] & 1) == 0 ? 1f : -1f;
            var weight = 1f + (hash[5] / 255f);

            embedding[bucket] += sign * weight;
        }

        Normalize(embedding);
        return embedding;
    }

    private static void Normalize(float[] vector)
    {
        double magnitudeSquared = 0;
        foreach (var value in vector)
        {
            magnitudeSquared += value * value;
        }

        if (magnitudeSquared <= 0)
        {
            vector[0] = 1f;
            return;
        }

        var magnitude = Math.Sqrt(magnitudeSquared);
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(vector[i] / magnitude);
        }
    }
}
