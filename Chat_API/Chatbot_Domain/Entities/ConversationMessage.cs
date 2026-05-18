namespace Chatbot_Domain.Entities
{
    public enum ConversationRole
    {
        User,
        Assistant
    }

    public class ConversationMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ConversationId { get; set; }
        public ConversationRole Role { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public Conversation? Conversation { get; set; }
    }
}
