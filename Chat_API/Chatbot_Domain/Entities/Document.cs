using System;
using System.Collections.Generic;

namespace Chatbot_Domain.Entities
{
    public enum DocumentStatus
    {
        Uploaded,
        Queued,
        Processing,
        Completed,
        Embedded,
        Failed
    }

    public class Document
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime UploadDate { get; set; } = DateTime.UtcNow;
        public DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;
        public string? ErrorMessage { get; set; }

        public virtual ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
    }
}
