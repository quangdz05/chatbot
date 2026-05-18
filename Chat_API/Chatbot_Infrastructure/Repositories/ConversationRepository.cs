using Chatbot_Application.Interfaces.Repositories;
using Chatbot_Domain.Entities;
using Chatbot_Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Chatbot_Infrastructure.Repositories
{
    public class ConversationRepository : IConversationRepository
    {
        private readonly AppDbContext _context;

        public ConversationRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Conversation> GetOrCreateAsync(Guid? conversationId)
        {
            if (conversationId.HasValue)
            {
                var existing = await _context.Conversations.FirstOrDefaultAsync(x => x.Id == conversationId.Value);
                if (existing != null)
                {
                    return existing;
                }
            }

            var conversation = new Conversation();
            await _context.Conversations.AddAsync(conversation);
            await _context.SaveChangesAsync();
            return conversation;
        }

        public async Task AddMessageAsync(Guid conversationId, ConversationRole role, string content)
        {
            var message = new ConversationMessage
            {
                ConversationId = conversationId,
                Role = role,
                Content = content,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _context.ConversationMessages.AddAsync(message);

            var conversation = await _context.Conversations.FirstAsync(x => x.Id == conversationId);
            conversation.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<ConversationMessage>> GetRecentMessagesAsync(Guid conversationId, int take)
        {
            return await _context.ConversationMessages
                .Where(x => x.ConversationId == conversationId)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(take)
                .OrderBy(x => x.CreatedAtUtc)
                .ToListAsync();
        }
    }
}
