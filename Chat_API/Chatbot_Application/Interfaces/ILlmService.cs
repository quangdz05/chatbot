using System.Threading.Tasks;

namespace Chatbot_Application.Interfaces
{
    public interface ILlmService
    {
        Task<string> GenerateResponseAsync(string systemPrompt, IReadOnlyList<DTOs.ChatHistoryMessage> history, string userMessage);
    }
}
