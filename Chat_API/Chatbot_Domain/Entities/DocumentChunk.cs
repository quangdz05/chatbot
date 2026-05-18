using System;
using System.Security.Cryptography;
using System.Text;
using Pgvector;

namespace Chatbot_Domain.Entities
{
    public enum ChunkEmbeddingStatus
    {
        Pending,
        Processing,
        Embedded,
        Failed
    }

    public class DocumentChunk
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DocumentId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string Heading { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        
        public Vector? Embedding { get; set; }

        /// <summary>SHA256 hash of Content – used to skip re-embedding identical chunks.</summary>
        public string? ContentHash { get; set; }

        /// <summary>Tracks the embedding status of this individual chunk.</summary>
        public ChunkEmbeddingStatus EmbeddingStatus { get; set; } = ChunkEmbeddingStatus.Pending;

        /// <summary>Error details when EmbeddingStatus == Failed.</summary>
        public string? ErrorMessage { get; set; }

        public virtual Document? Document { get; set; }

        /// <summary>Compute SHA256 hash of the Content property.</summary>
        public static string ComputeHash(string content)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
            return Convert.ToHexStringLower(bytes);
        }
    }
}
