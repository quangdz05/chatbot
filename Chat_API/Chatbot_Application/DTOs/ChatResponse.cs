namespace Chatbot_Application.DTOs
{
    public class ChatResponse
    {
        public string Answer { get; set; } = string.Empty;
        public bool IsFromKnowledgeBase { get; set; }
        public List<ChatSource> Sources { get; set; } = new();
        public Guid ConversationId { get; set; }
    }

    public class ChatSource
    {
        public Guid DocumentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        public string Heading { get; set; } = string.Empty;
        public string Snippet { get; set; } = string.Empty;
        public double Similarity { get; set; }
    }

    public class RetrievedChunk
    {
        public Guid DocumentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        public string Heading { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public double Similarity { get; set; }
    }
}
