using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chatbot_Domain.Entities;
using Pgvector;

namespace Chatbot_Application.Interfaces.Repositories
{
    public interface IDocumentChunkRepository
    {
        Task AddRangeAsync(IEnumerable<DocumentChunk> chunks);
        Task UpdateRangeAsync(IEnumerable<DocumentChunk> chunks);
        Task<IEnumerable<(DocumentChunk Chunk, double Distance)>> SearchSimilarAsync(Vector queryEmbedding, int limit = 5);
        Task DeleteByDocumentIdAsync(Guid documentId);
        Task<int> CountByDocumentIdAsync(Guid documentId);

        /// <summary>Returns content hashes that already have an embedding in the database.</summary>
        Task<HashSet<string>> GetExistingEmbeddedHashesAsync(IEnumerable<string> hashes);

        /// <summary>Returns chunks for a document that still need embedding.</summary>
        Task<List<DocumentChunk>> GetPendingChunksByDocumentIdAsync(Guid documentId);
    }
}
