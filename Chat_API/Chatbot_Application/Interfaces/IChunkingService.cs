namespace Chatbot_Application.Interfaces
{
    /// <summary>
    /// Chia nội dung trang PDF thành các chunk ngữ nghĩa dựa trên heading và kích thước.
    /// </summary>
    public interface IChunkingService
    {
        List<ChunkResult> ChunkPages(List<PageContent> pages);
    }

    public class ChunkResult
    {
        public string Content { get; set; } = string.Empty;
        public string Heading { get; set; } = string.Empty;
        public int PageNumber { get; set; }
    }
}
