using Chatbot_Application.DTOs;

namespace Chatbot_Application.Interfaces
{
    public interface IDocumentProcessingService
    {
        Task<UploadResponse> ProcessDocumentAsync(string filePath, string fileName);
        Task<UploadResponse> ProcessExistingDocumentAsync(Guid documentId, string filePath, string fileName);
    }
}
