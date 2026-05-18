using System.ComponentModel.DataAnnotations;

namespace Chatbot_Application.DTOs
{
    public class ChatRequest
    {
        [Required]
        [MinLength(1)]
        public string Message { get; set; } = string.Empty;
        public Guid? ConversationId { get; set; }
    }
}
