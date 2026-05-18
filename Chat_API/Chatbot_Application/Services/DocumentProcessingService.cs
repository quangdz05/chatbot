using Chatbot_Application.DTOs;
using Chatbot_Application.Interfaces;
using Chatbot_Application.Interfaces.Repositories;
using Chatbot_Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Chatbot_Application.Services
{
    public class DocumentProcessingService : IDocumentProcessingService
    {
        private readonly IPdfParserService _pdfParser;
        private readonly IChunkingService _chunkingService;
        private readonly IEmbeddingService _embeddingService;
        private readonly IDocumentRepository _documentRepository;
        private readonly IDocumentChunkRepository _chunkRepository;
        private readonly ILogger<DocumentProcessingService> _logger;

        /// <summary>Number of chunks per embedding API batch call.</summary>
        private const int EmbeddingBatchSize = 25;

        /// <summary>Delay between embedding batch calls to avoid rate limiting.</summary>
        private const int DelayBetweenBatchesMs = 2000;

        public DocumentProcessingService(
            IPdfParserService pdfParser,
            IChunkingService chunkingService,
            IEmbeddingService embeddingService,
            IDocumentRepository documentRepository,
            IDocumentChunkRepository chunkRepository,
            ILogger<DocumentProcessingService> logger)
        {
            _pdfParser = pdfParser;
            _chunkingService = chunkingService;
            _embeddingService = embeddingService;
            _documentRepository = documentRepository;
            _chunkRepository = chunkRepository;
            _logger = logger;
        }

        public async Task<UploadResponse> ProcessDocumentAsync(string filePath, string fileName)
        {
            var existing = await _documentRepository.GetByFileNameAsync(fileName);
            if (existing != null)
            {
                var existingChunkCount = await _chunkRepository.CountByDocumentIdAsync(existing.Id);
                if (existingChunkCount > 0 && existing.Status == DocumentStatus.Completed)
                {
                    return new UploadResponse
                    {
                        DocumentId = existing.Id,
                        FileName = existing.FileName,
                Status = existing.Status.ToString(),
                        ChunkCount = existingChunkCount
                    };
                }

                return await ProcessExistingDocumentAsync(existing.Id, filePath, fileName);
            }

            var document = new Document
            {
                FileName = fileName,
                FilePath = filePath,
                Status = DocumentStatus.Queued
            };
            await _documentRepository.AddAsync(document);
            return await ProcessExistingDocumentAsync(document.Id, filePath, fileName);
        }

        public async Task<UploadResponse> ProcessExistingDocumentAsync(Guid documentId, string filePath, string fileName)
        {
            var document = await _documentRepository.GetByIdAsync(documentId)
                ?? throw new InvalidOperationException($"Document {documentId} not found.");

            document.Status = DocumentStatus.Processing;
            document.FilePath = filePath;
            document.ErrorMessage = null;
            await _documentRepository.UpdateAsync(document);

            try
            {
                // ── Step 1: Parse PDF and chunk ──
                await _chunkRepository.DeleteByDocumentIdAsync(document.Id);

                var pages = await _pdfParser.ParseAsync(filePath);
                var chunkResults = _chunkingService.ChunkPages(pages);

                _logger.LogInformation(
                    "Document {DocumentId} ({FileName}): {ChunkCount} chunks from {PageCount} pages",
                    document.Id, fileName, chunkResults.Count, pages.Count);

                // ── Step 2: Build DocumentChunk list with ContentHash ──
                var documentChunks = chunkResults.Select(c => new DocumentChunk
                {
                    DocumentId = document.Id,
                    Content = c.Content,
                    Heading = c.Heading,
                    PageNumber = c.PageNumber,
                    ContentHash = DocumentChunk.ComputeHash(c.Content),
                    EmbeddingStatus = ChunkEmbeddingStatus.Pending
                }).ToList();

                // ── Step 3: Check for already-embedded identical content ──
                var allHashes = documentChunks
                    .Where(c => c.ContentHash != null)
                    .Select(c => c.ContentHash!)
                    .Distinct()
                    .ToList();

                var existingHashes = await _chunkRepository.GetExistingEmbeddedHashesAsync(allHashes);

                if (existingHashes.Count > 0)
                {
                    _logger.LogInformation(
                        "Skipping {SkipCount} chunks with existing embeddings (content hash match)",
                        documentChunks.Count(c => c.ContentHash != null && existingHashes.Contains(c.ContentHash)));
                }

                // Save all chunks to DB first (Pending status)
                await _chunkRepository.AddRangeAsync(documentChunks);

                // ── Step 4: Embed in batches ──
                var chunksToEmbed = documentChunks
                    .Where(c => c.ContentHash == null || !existingHashes.Contains(c.ContentHash))
                    .ToList();

                var skippedChunks = documentChunks
                    .Where(c => c.ContentHash != null && existingHashes.Contains(c.ContentHash))
                    .ToList();

                // Mark skipped chunks as Embedded immediately (content already known)
                foreach (var chunk in skippedChunks)
                {
                    chunk.EmbeddingStatus = ChunkEmbeddingStatus.Embedded;
                }

                int embeddedCount = skippedChunks.Count;
                int failedCount = 0;

                for (int batchStart = 0; batchStart < chunksToEmbed.Count; batchStart += EmbeddingBatchSize)
                {
                    var batch = chunksToEmbed
                        .Skip(batchStart)
                        .Take(EmbeddingBatchSize)
                        .ToList();

                    try
                    {
                        // Mark batch as Processing
                        foreach (var chunk in batch)
                        {
                            chunk.EmbeddingStatus = ChunkEmbeddingStatus.Processing;
                        }

                        var contents = batch.Select(c => c.Content).ToList();
                        var embeddings = await _embeddingService.GenerateBatchEmbeddingAsync(contents);

                        for (int i = 0; i < batch.Count; i++)
                        {
                            batch[i].Embedding = embeddings[i];
                            batch[i].EmbeddingStatus = ChunkEmbeddingStatus.Embedded;
                            batch[i].ErrorMessage = null;
                        }

                        embeddedCount += batch.Count;

                        _logger.LogInformation(
                            "Embedded batch {Start}-{End} of {Total} for document {DocumentId}",
                            batchStart + 1,
                            Math.Min(batchStart + EmbeddingBatchSize, chunksToEmbed.Count),
                            chunksToEmbed.Count,
                            document.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to embed batch {Start}-{End} for document {DocumentId}",
                            batchStart + 1,
                            Math.Min(batchStart + EmbeddingBatchSize, chunksToEmbed.Count),
                            document.Id);

                        foreach (var chunk in batch)
                        {
                            chunk.EmbeddingStatus = ChunkEmbeddingStatus.Failed;
                            chunk.ErrorMessage = ex.Message.Length > 2000
                                ? ex.Message[..2000]
                                : ex.Message;
                        }

                        failedCount += batch.Count;
                    }

                    // Delay between batches to avoid rate limiting (skip after last batch)
                    if (batchStart + EmbeddingBatchSize < chunksToEmbed.Count)
                    {
                        await Task.Delay(DelayBetweenBatchesMs);
                    }
                }

                // ── Step 5: Persist all embedding results ──
                await _chunkRepository.UpdateRangeAsync(documentChunks);

                // Update document status
                if (failedCount == 0)
                {
                    document.Status = DocumentStatus.Completed;
                    document.ErrorMessage = null;
                }
                else if (embeddedCount > 0)
                {
                    // Partial success
                    document.Status = DocumentStatus.Completed;
                    document.ErrorMessage = $"{failedCount} of {documentChunks.Count} chunks failed to embed.";
                }
                else
                {
                    document.Status = DocumentStatus.Failed;
                    document.ErrorMessage = $"All {failedCount} chunks failed to embed.";
                }

                await _documentRepository.UpdateAsync(document);

                return new UploadResponse
                {
                    DocumentId = document.Id,
                    FileName = fileName,
                    Status = document.Status.ToString(),
                    ChunkCount = documentChunks.Count
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process document {DocumentId} ({FileName})", document.Id, fileName);
                document.Status = DocumentStatus.Failed;
                document.ErrorMessage = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                await _documentRepository.UpdateAsync(document);
                throw;
            }
        }
    }
}
