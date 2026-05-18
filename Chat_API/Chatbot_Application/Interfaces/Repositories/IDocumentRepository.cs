using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chatbot_Domain.Entities;

namespace Chatbot_Application.Interfaces.Repositories
{
    public interface IDocumentRepository
    {
        Task<Document?> GetByIdAsync(Guid id);
        Task<Document?> GetByFileNameAsync(string fileName);
        Task<IEnumerable<Document>> GetAllAsync();
        Task AddAsync(Document document);
        Task UpdateAsync(Document document);
        Task DeleteAsync(Guid id);
    }
}
