using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chatbot_Application.Interfaces.Repositories;
using Chatbot_Domain.Entities;
using Chatbot_Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Chatbot_Infrastructure.Repositories
{
    public class DocumentRepository : IDocumentRepository
    {
        private readonly AppDbContext _context;

        public DocumentRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Document?> GetByIdAsync(Guid id)
        {
            return await _context.Documents
                .Include(d => d.Chunks)
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task<Document?> GetByFileNameAsync(string fileName)
        {
            return await _context.Documents
                .Include(d => d.Chunks)
                .FirstOrDefaultAsync(d => d.FileName == fileName);
        }

        public async Task<IEnumerable<Document>> GetAllAsync()
        {
            return await _context.Documents.ToListAsync();
        }

        public async Task AddAsync(Document document)
        {
            await _context.Documents.AddAsync(document);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Document document)
        {
            _context.Documents.Update(document);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var document = await _context.Documents
                .Include(d => d.Chunks)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
            {
                return;
            }

            _context.DocumentChunks.RemoveRange(document.Chunks);
            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();
        }
    }
}

