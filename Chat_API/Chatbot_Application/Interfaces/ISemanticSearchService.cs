using System.Collections.Generic;
using System.Threading.Tasks;
using Chatbot_Application.DTOs;

namespace Chatbot_Application.Interfaces
{
    public interface ISemanticSearchService
    {
        Task<IEnumerable<RetrievedChunk>> SearchAsync(string query, int limit = 5, double minSimilarity = 0.6);
    }
}
