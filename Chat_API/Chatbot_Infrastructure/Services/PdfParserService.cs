using Chatbot_Application.Interfaces;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace Chatbot_Infrastructure.Services
{
    /// <summary>
    /// Trích xuất text từ PDF sử dụng PdfPig (yêu cầu PDF có lớp text).
    /// </summary>
    public class PdfParserService : IPdfParserService
    {
        private readonly ILogger<PdfParserService> _logger;

        public PdfParserService(ILogger<PdfParserService> logger)
        {
            _logger = logger;
        }

        public async Task<List<PageContent>> ParseAsync(string filePath)
        {
            var pages = new List<PageContent>();

            try
            {
                // Task.Run để tránh block thread vì PdfPig là thư viện đồng bộ
                await Task.Run(() =>
                {
                    using (var document = PdfDocument.Open(filePath))
                    {
                        foreach (var page in document.GetPages())
                        {
                            var text = page.Text;
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                pages.Add(new PageContent
                                {
                                    PageNumber = page.Number,
                                    Text = text.Trim()
                                });
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đọc file PDF bằng PdfPig.");
                throw;
            }

            if (pages.Count == 0)
            {
                _logger.LogWarning("⚠️ Không tìm thấy text nào trong file PDF. Đảm bảo đây không phải là PDF dạng scan.");
            }
            else
            {
                _logger.LogInformation("✅ Trích xuất thành công {Count} trang text từ file PDF.", pages.Count);
            }

            return pages;
        }
    }
}
