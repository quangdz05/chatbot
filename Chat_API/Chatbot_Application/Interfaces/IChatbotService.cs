using Chatbot_Application.DTOs;

namespace Chatbot_Application.Interfaces
{
    public interface IChatbotService
    {
        Task<ChatResponse> GetResponseAsync(ChatRequest request);
    }
}
