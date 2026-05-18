using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Chatbot_Application.Interfaces.Repositories;
using Chatbot_Domain.Entities;
using Chatbot_Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Chatbot_Infrastructure.Repositories
{
    public class DocumentChunkRepository : IDocumentChunkRepository
    {
        private readonly AppDbContext _context;

        public DocumentChunkRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddRangeAsync(IEnumerable<DocumentChunk> chunks)
        {
            await _context.DocumentChunks.AddRangeAsync(chunks);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteByDocumentIdAsync(Guid documentId)
        {
            var chunks = await _context.DocumentChunks.Where(c => c.DocumentId == documentId).ToListAsync();
            _context.DocumentChunks.RemoveRange(chunks);
            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<(DocumentChunk Chunk, double Distance)>> SearchSimilarAsync(Vector queryEmbedding, int limit = 5)
        {
            var ranked = await _context.DocumentChunks
                .Include(c => c.Document)
                .Select(c => new
                {
                    Chunk = c,
                    Distance = c.Embedding != null ? c.Embedding.CosineDistance(queryEmbedding) : 1.0
                })
                .OrderBy(x => x.Distance)
                .Take(limit)
                .ToListAsync();

            return ranked.Select(x => (x.Chunk, x.Distance));
        }

        public async Task<int> CountByDocumentIdAsync(Guid documentId)
        {
            return await _context.DocumentChunks.CountAsync(c => c.DocumentId == documentId);
        }

        public async Task UpdateRangeAsync(IEnumerable<DocumentChunk> chunks)
        {
            _context.DocumentChunks.UpdateRange(chunks);
            await _context.SaveChangesAsync();
        }

        public async Task<HashSet<string>> GetExistingEmbeddedHashesAsync(IEnumerable<string> hashes)
        {
            var hashList = hashes.ToList();
            if (hashList.Count == 0) return new HashSet<string>();

            var found = await _context.DocumentChunks
                .Where(c => c.ContentHash != null
                    && hashList.Contains(c.ContentHash)
                    && c.Embedding != null
                    && c.EmbeddingStatus == ChunkEmbeddingStatus.Embedded)
                .Select(c => c.ContentHash!)
                .Distinct()
                .ToListAsync();

            return new HashSet<string>(found, StringComparer.OrdinalIgnoreCase);
        }

        public async Task<List<DocumentChunk>> GetPendingChunksByDocumentIdAsync(Guid documentId)
        {
            return await _context.DocumentChunks
                .Where(c => c.DocumentId == documentId
                    && (c.EmbeddingStatus == ChunkEmbeddingStatus.Pending || c.EmbeddingStatus == ChunkEmbeddingStatus.Failed))
                .ToListAsync();
        }
    }
}
