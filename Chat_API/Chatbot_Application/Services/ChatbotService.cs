using System.Text;
using Chatbot_Application.DTOs;
using Chatbot_Application.Interfaces;
using Chatbot_Application.Interfaces.Repositories;
using Chatbot_Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace Chatbot_Application.Services
{
    public class ChatbotService : IChatbotService
    {
        private const string WelcomeMessage = "Xin chào! 📈\nTôi là trợ lý AI hỗ trợ thông tin chứng khoán và thị trường tài chính.\n\nBạn có thể hỏi về:\n• Mã cổ phiếu\n• Xu hướng thị trường\n• Thông tin doanh nghiệp\n• Chỉ số tài chính\n• Tin tức và phân tích cơ bản\n\nBạn muốn tìm hiểu mã cổ phiếu nào hôm nay?";

        private readonly ISemanticSearchService _semanticSearchService;
        private readonly ILlmService _llmService;
        private readonly IConversationRepository _conversationRepository;
        private readonly IConfiguration _configuration;

        public ChatbotService(
            ISemanticSearchService semanticSearchService,
            ILlmService llmService,
            IConversationRepository conversationRepository,
            IConfiguration configuration)
        {
            _semanticSearchService = semanticSearchService;
            _llmService = llmService;
            _conversationRepository = conversationRepository;
            _configuration = configuration;
        }

        public async Task<ChatResponse> GetResponseAsync(ChatRequest request)
        {
            var topK = _configuration.GetValue<int?>("Rag:TopK") ?? 3;
            var minSimilarity = _configuration.GetValue<double?>("Rag:MinSimilarity") ?? 0.6;
            var fallback = BuildFallbackMessage();
            var conversation = await _conversationRepository.GetOrCreateAsync(request.ConversationId);

            if (IsWelcomeTrigger(request.Message))
            {
                await _conversationRepository.AddMessageAsync(conversation.Id, ConversationRole.User, request.Message);
                await _conversationRepository.AddMessageAsync(conversation.Id, ConversationRole.Assistant, WelcomeMessage);

                return new ChatResponse
                {
                    Answer = WelcomeMessage,
                    IsFromKnowledgeBase = false,
                    ConversationId = conversation.Id
                };
            }

            var relevantChunks = (await _semanticSearchService.SearchAsync(request.Message, topK, 0.0)).ToList();

            if (relevantChunks.Count == 0 || relevantChunks.Max(c => c.Similarity) < minSimilarity)
            {
                await _conversationRepository.AddMessageAsync(conversation.Id, ConversationRole.User, request.Message);
                await _conversationRepository.AddMessageAsync(conversation.Id, ConversationRole.Assistant, fallback);

                return new ChatResponse
                {
                    Answer = fallback,
                    IsFromKnowledgeBase = false,
                    ConversationId = conversation.Id
                };
            }

            var historyTake = _configuration.GetValue<int?>("Rag:HistoryMessageCount") ?? 6;
            var maxHistoryChars = _configuration.GetValue<int?>("Rag:MaxHistoryChars") ?? 2000;
            var history = await _conversationRepository.GetRecentMessagesAsync(conversation.Id, historyTake);
            var historyForPrompt = BuildHistoryForPrompt(history, maxHistoryChars);

            var systemPrompt = BuildSystemPrompt(relevantChunks, fallback);
            var answer = await _llmService.GenerateResponseAsync(systemPrompt, historyForPrompt, request.Message);
            answer = NormalizeToVietnameseFallback(answer, fallback);

            await _conversationRepository.AddMessageAsync(conversation.Id, ConversationRole.User, request.Message);
            await _conversationRepository.AddMessageAsync(conversation.Id, ConversationRole.Assistant, answer);

            return new ChatResponse
            {
                Answer = answer,
                IsFromKnowledgeBase = true,
                ConversationId = conversation.Id,
                Sources = relevantChunks.Select(x => new ChatSource
                {
                    DocumentId = x.DocumentId,
                    FileName = x.FileName,
                    PageNumber = x.PageNumber,
                    Heading = x.Heading,
                    Snippet = x.Content.Length <= 240 ? x.Content : x.Content[..240],
                    Similarity = x.Similarity
                }).ToList()
            };
        }

        private IReadOnlyList<ChatHistoryMessage> BuildHistoryForPrompt(IReadOnlyList<ConversationMessage> history, int maxChars)
        {
            var result = new List<ChatHistoryMessage>();
            var totalChars = 0;

            foreach (var item in history)
            {
                if (totalChars + item.Content.Length > maxChars)
                {
                    continue;
                }

                result.Add(new ChatHistoryMessage
                {
                    Role = item.Role == ConversationRole.User ? "user" : "assistant",
                    Content = item.Content
                });
                totalChars += item.Content.Length;
            }

            return result;
        }

        private string BuildFallbackMessage()
        {
            var hotline = _configuration["Support:Hotline"] ?? "";
            var template = _configuration["Rag:NoContextFallback"]
                ?? "Thong tin nay hien chua co trong tai lieu ho tro. Vui long lien he hotline: [SO HOTLINE] de duoc ho tro them.";

            return template.Replace("[S? HOTLINE]", hotline).Replace("[SO HOTLINE]", hotline);
        }

        private string BuildSystemPrompt(IEnumerable<RetrievedChunk> contextChunks, string fallbackMessage)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Ban la tro ly truy xuat tai lieu noi bo.");
            sb.AppendLine("Luon tra loi bang tieng Viet tu nhien, ro rang, de hieu.");
            sb.AppendLine("Chi duoc su dung thong tin trong CONTEXT, khong duoc tu bo sung kien thuc ben ngoai.");
            sb.AppendLine($"Neu CONTEXT khong du de tra loi, phai tra loi CHINH XAC cau sau: \"{fallbackMessage}\"");
            sb.AppendLine("Khong duoc tra loi fallback bang ngon ngu khac.");
            sb.AppendLine("--- CONTEXT START ---");

            foreach (var chunk in contextChunks)
            {
                sb.AppendLine($"[File={chunk.FileName}; Page={chunk.PageNumber}; Heading={chunk.Heading}; Similarity={chunk.Similarity:F3}]");
                sb.AppendLine(chunk.Content);
                sb.AppendLine("---");
            }

            sb.AppendLine("--- CONTEXT END ---");
            return sb.ToString();
        }

        private static string NormalizeToVietnameseFallback(string answer, string fallback)
        {
            if (string.IsNullOrWhiteSpace(answer))
            {
                return fallback;
            }

            var normalized = answer.Trim().ToLowerInvariant();
            if (normalized.Contains("context provided is insufficient")
                || normalized.Contains("insufficient to answer")
                || normalized.Contains("insufficient context")
                || normalized.Contains("not enough context")
                || normalized.Contains("cannot answer from the context"))
            {
                return fallback;
            }

            return answer;
        }

        private static bool IsWelcomeTrigger(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            var normalized = message.Trim().ToLowerInvariant();
            return normalized == "hello"
                || normalized == "xin chao"
                || normalized == "xin chào"
                || normalized == "toi can tro giup"
                || normalized == "toi can ho tro"
                || normalized == "toi can tro giup."
                || normalized == "tôi cần trợ giúp"
                || normalized == "tôi cần hỗ trợ";
        }
    }
}
