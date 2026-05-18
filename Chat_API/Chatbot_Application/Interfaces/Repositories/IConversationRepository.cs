using Chatbot_Domain.Entities;

namespace Chatbot_Application.Interfaces.Repositories
{
    public interface IConversationRepository
    {
        Task<Conversation> GetOrCreateAsync(Guid? conversationId);
        Task AddMessageAsync(Guid conversationId, ConversationRole role, string content);
        Task<IReadOnlyList<ConversationMessage>> GetRecentMessagesAsync(Guid conversationId, int take);
    }
}
