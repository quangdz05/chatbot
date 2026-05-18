namespace Chatbot_Application.Interfaces
{
    /// <summary>
    /// Trích xuất text thô từ file PDF, trả về nội dung theo từng trang.
    /// </summary>
    public interface IPdfParserService
    {
        Task<List<PageContent>> ParseAsync(string filePath);
    }

    public class PageContent
    {
        public int PageNumber { get; set; }
        public string Text { get; set; } = string.Empty;
    }
}
