using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Chatbot_Application.DTOs;
using Chatbot_Application.Interfaces;
using Chatbot_Application.Interfaces.Repositories;

namespace Chatbot_Application.Services
{
    public class SemanticSearchService : ISemanticSearchService
    {
        private readonly IDocumentChunkRepository _chunkRepository;
        private readonly IEmbeddingService _embeddingService;

        public SemanticSearchService(
            IDocumentChunkRepository chunkRepository,
            IEmbeddingService embeddingService)
        {
            _chunkRepository = chunkRepository;
            _embeddingService = embeddingService;
        }

        public async Task<IEnumerable<RetrievedChunk>> SearchAsync(string query, int limit = 5, double minSimilarity = 0.6)
        {
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
            var chunks = await _chunkRepository.SearchSimilarAsync(queryEmbedding, limit);

            return chunks
                .Select(x => new RetrievedChunk
                {
                    DocumentId = x.Chunk.DocumentId,
                    FileName = x.Chunk.Document?.FileName ?? string.Empty,
                    PageNumber = x.Chunk.PageNumber,
                    Heading = x.Chunk.Heading,
                    Content = x.Chunk.Content,
                    Similarity = 1.0 - x.Distance
                })
                .Where(x => x.Similarity >= minSimilarity)
                .ToList();
        }
    }
}
