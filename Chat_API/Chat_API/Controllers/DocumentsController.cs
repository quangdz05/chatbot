using Chat_API.Background;
using Chatbot_Application.DTOs;
using Chatbot_Application.Interfaces.Repositories;
using Chatbot_Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chat_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly IDocumentChunkRepository _chunkRepository;
        private readonly IDocumentIngestionQueue _queue;
        private readonly IConfiguration _configuration;

        public DocumentsController(
            IDocumentRepository documentRepository,
            IDocumentChunkRepository chunkRepository,
            IDocumentIngestionQueue queue,
            IConfiguration configuration)
        {
            _documentRepository = documentRepository;
            _chunkRepository = chunkRepository;
            _queue = queue;
            _configuration = configuration;
        }

        [HttpPost("upload")]
        [RequestSizeLimit(50_000_000)]
        public async Task<ActionResult<DocumentStatusResponse>> Upload([FromForm(Name = "file")] IFormFile file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(new { error = "invalid_file", message = "File is required." });
            }

            if (!Path.GetExtension(file.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "invalid_file_type", message = "Only PDF files are supported." });
            }

            var existing = await _documentRepository.GetByFileNameAsync(file.FileName);
            if (existing != null)
            {
                var chunkCount = await _chunkRepository.CountByDocumentIdAsync(existing.Id);
                if (existing.Status == DocumentStatus.Completed && chunkCount > 0)
                {
                    return Ok(ToStatusResponse(existing));
                }
            }

            var uploadRoot = _configuration["UploadSettings:RootPath"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
            Directory.CreateDirectory(uploadRoot);

            var safeFileName = Path.GetFileName(file.FileName);
            var outputPath = Path.Combine(uploadRoot, safeFileName);

            await using (var stream = System.IO.File.Create(outputPath))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var document = existing ?? new Document
            {
                FileName = safeFileName,
                FilePath = outputPath,
                Status = DocumentStatus.Queued,
                UploadDate = DateTime.UtcNow
            };

            document.FilePath = outputPath;
            document.Status = DocumentStatus.Queued;

            if (existing == null)
            {
                await _documentRepository.AddAsync(document);
            }
            else
            {
                await _documentRepository.UpdateAsync(document);
            }

            await _queue.QueueAsync(new DocumentIngestionJob(document.Id, outputPath, safeFileName), cancellationToken);
            return Accepted(ToStatusResponse(document));
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var documents = await _documentRepository.GetAllAsync();
            return Ok(documents.Select(ToStatusResponse));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
            {
                return NotFound(new { error = "document_not_found", message = "Document not found." });
            }

            await _chunkRepository.DeleteByDocumentIdAsync(id);
            await _documentRepository.DeleteAsync(id);

            if (!string.IsNullOrWhiteSpace(document.FilePath) && System.IO.File.Exists(document.FilePath))
            {
                System.IO.File.Delete(document.FilePath);
            }

            return NoContent();
        }

        [HttpGet("{id:guid}/status")]
        public async Task<ActionResult<DocumentStatusResponse>> GetStatus(Guid id)
        {
            var document = await _documentRepository.GetByIdAsync(id);
            if (document == null)
            {
                return NotFound(new { error = "document_not_found", message = "Document not found." });
            }

            return Ok(ToStatusResponse(document));
        }

        private static DocumentStatusResponse ToStatusResponse(Document document)
        {
            return new DocumentStatusResponse
            {
                DocumentId = document.Id,
                FileName = document.FileName,
                Status = document.Status.ToString(),
                UploadDate = document.UploadDate
            };
        }
    }
}
