using System.Text;
using System.Text.Json;
using Chatbot_Application.DTOs;
using Chatbot_Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Chatbot_Infrastructure.Services
{
    public class GeminiLlmService : ILlmService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;

        public GeminiLlmService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["AiSettings:ApiKey"]
                ?? throw new ArgumentNullException("AiSettings:ApiKey is not configured");
            _model = configuration["AiSettings:ChatModel"] ?? "gemini-2.0-flash";
        }

        public async Task<string> GenerateResponseAsync(string systemPrompt, IReadOnlyList<ChatHistoryMessage> history, string userMessage)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
            var mergedHistory = string.Join("\n", history.Select(h => $"{h.Role}: {h.Content}"));

            var request = new
            {
                system_instruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = $"{mergedHistory}\n\n{userMessage}" } }
                    }
                }
            };

            var json = JsonSerializer.Serialize(request);
            using var response = await _httpClient.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()
                ?? string.Empty;
        }
    }
}
