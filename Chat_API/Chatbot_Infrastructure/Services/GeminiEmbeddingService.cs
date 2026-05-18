using System.Net;
using System.Text;
using System.Text.Json;
using Chatbot_Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pgvector;

namespace Chatbot_Infrastructure.Services
{
    public class GeminiEmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly int _outputDimensionality;
        private readonly ILogger<GeminiEmbeddingService> _logger;

        // Rate-limit / batch settings
        private const int MaxBatchSize = 25;         // Gemini batchEmbedContents supports up to 100, we use 25 for safety
        private const int MaxRetries = 5;
        private const double BaseDelaySeconds = 2.0; // exponential backoff base
        private const int DelayBetweenBatchesMs = 2000; // 2 seconds between batches

        public GeminiEmbeddingService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiEmbeddingService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["AiSettings:ApiKey"]
                ?? throw new ArgumentNullException("AiSettings:ApiKey is not configured");
            _model = NormalizeModel(configuration["AiSettings:EmbeddingModel"] ?? "gemini-embedding-001");
            _outputDimensionality = configuration.GetValue<int?>("AiSettings:EmbeddingDimension") ?? 768;
        }

        public async Task<Vector> GenerateEmbeddingAsync(string text)
        {
            var result = await GenerateBatchEmbeddingAsync(new[] { text });
            return result[0];
        }

        /// <summary>
        /// Generates embeddings for a list of texts using Gemini batchEmbedContents API.
        /// Automatically splits into sub-batches, retries on 429, and adds delay between batches.
        /// </summary>
        public async Task<IReadOnlyList<Vector>> GenerateBatchEmbeddingAsync(IReadOnlyList<string> texts)
        {
            if (texts.Count == 0) return Array.Empty<Vector>();

            var allVectors = new Vector[texts.Count];

            // Split texts into sub-batches
            for (int batchStart = 0; batchStart < texts.Count; batchStart += MaxBatchSize)
            {
                var batchEnd = Math.Min(batchStart + MaxBatchSize, texts.Count);
                var batchTexts = texts.Skip(batchStart).Take(batchEnd - batchStart).ToList();

                _logger.LogInformation(
                    "Embedding batch {Start}-{End} of {Total} chunks",
                    batchStart + 1, batchEnd, texts.Count);

                var batchVectors = await RequestBatchEmbeddingWithRetryAsync(_model, batchTexts);

                for (int i = 0; i < batchVectors.Count; i++)
                {
                    allVectors[batchStart + i] = batchVectors[i];
                }

                // Delay between batches to avoid rate limit (skip after last batch)
                if (batchEnd < texts.Count)
                {
                    _logger.LogDebug("Waiting {DelayMs}ms before next batch...", DelayBetweenBatchesMs);
                    await Task.Delay(DelayBetweenBatchesMs);
                }
            }

            return allVectors;
        }

        /// <summary>
        /// Calls Gemini batchEmbedContents with exponential backoff retry on 429.
        /// </summary>
        private async Task<IReadOnlyList<Vector>> RequestBatchEmbeddingWithRetryAsync(string model, List<string> texts)
        {
            var modelPath = $"models/{model}";
            var url = $"https://generativelanguage.googleapis.com/v1beta/{modelPath}:batchEmbedContents";

            // Build batch request payload
            var requests = texts.Select(text => new
            {
                model = modelPath,
                content = new { parts = new[] { new { text } } },
                output_dimensionality = _outputDimensionality
            }).ToArray();

            var requestPayload = new { requests };
            var json = JsonSerializer.Serialize(requestPayload);

            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("x-goog-api-key", _apiKey);

                using var response = await _httpClient.SendAsync(request);
                var responseJson = await response.Content.ReadAsStringAsync();

                // Handle 429 Too Many Requests with exponential backoff
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    if (attempt >= MaxRetries)
                    {
                        _logger.LogError(
                            "Rate limit exceeded after {MaxRetries} retries. Response: {Response}",
                            MaxRetries, responseJson);
                        throw new HttpRequestException(
                            $"Gemini embedding rate limit exceeded after {MaxRetries} retries.");
                    }

                    var delaySeconds = BaseDelaySeconds * Math.Pow(2, attempt);
                    // Add jitter (±20%)
                    var jitter = delaySeconds * (Random.Shared.NextDouble() * 0.4 - 0.2);
                    var totalDelay = TimeSpan.FromSeconds(delaySeconds + jitter);

                    _logger.LogWarning(
                        "Rate limited (429). Retry {Attempt}/{MaxRetries} after {Delay:F1}s",
                        attempt + 1, MaxRetries, totalDelay.TotalSeconds);

                    await Task.Delay(totalDelay);
                    continue;
                }

                // Handle model not found – fallback to default
                if (response.StatusCode == HttpStatusCode.NotFound
                    && !string.Equals(model, "gemini-embedding-001", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Model {Model} not found, falling back to gemini-embedding-001", model);
                    return await RequestBatchEmbeddingWithRetryAsync("gemini-embedding-001", texts);
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Gemini batch embedding failed ({(int)response.StatusCode} {response.ReasonPhrase}). Model: {model}. Response: {responseJson}");
                }

                // Parse batch response
                using var doc = JsonDocument.Parse(responseJson);
                var embeddings = doc.RootElement.GetProperty("embeddings");
                var vectors = new List<Vector>(texts.Count);

                foreach (var embedding in embeddings.EnumerateArray())
                {
                    var values = embedding.GetProperty("values").EnumerateArray()
                        .Select(e => e.GetSingle()).ToArray();
                    vectors.Add(new Vector(values));
                }

                return vectors;
            }

            // Should not reach here
            throw new HttpRequestException("Unexpected: exhausted all retry attempts.");
        }

        private static string NormalizeModel(string model)
        {
            var normalized = model.Trim();
            if (normalized.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized["models/".Length..];
            }

            // Legacy config value that may return 404 on embedContent.
            if (string.Equals(normalized, "text-embedding-004", StringComparison.OrdinalIgnoreCase))
            {
                return "gemini-embedding-001";
            }

            return normalized;
        }
    }
}
