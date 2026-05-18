using System.Threading.Tasks;
using Pgvector;

namespace Chatbot_Application.Interfaces
{
    public interface IEmbeddingService
    {
        Task<Vector> GenerateEmbeddingAsync(string text);
        Task<IReadOnlyList<Vector>> GenerateBatchEmbeddingAsync(IReadOnlyList<string> texts);
    }
}
