namespace Chatbot_Application.DTOs
{
    public class UploadResponse
    {
        public Guid DocumentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int ChunkCount { get; set; }
    }

    public class DocumentStatusResponse
    {
        public Guid DocumentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime UploadDate { get; set; }
    }
}
