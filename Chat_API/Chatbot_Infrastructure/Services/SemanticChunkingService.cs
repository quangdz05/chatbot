using System.Text;
using System.Text.RegularExpressions;
using Chatbot_Application.Interfaces;

namespace Chatbot_Infrastructure.Services
{
    /// <summary>
    /// Chia nội dung PDF thành các chunk ngữ nghĩa:
    /// 1. Phát hiện heading (dòng ngắn, viết hoa, bắt đầu bằng số/chữ cái + dấu chấm)
    /// 2. Chia chunk theo heading, nếu chunk quá lớn thì chia nhỏ theo paragraph
    /// </summary>
    public class SemanticChunkingService : IChunkingService
    {
        private const int MaxChunkSize = 1000;  // ký tự tối đa mỗi chunk
        private const int MinChunkSize = 100;   // ký tự tối thiểu (tránh chunk quá nhỏ)

        // Pattern nhận diện heading: bắt đầu bằng số/chữ La Mã + dấu chấm, hoặc dòng ngắn viết hoa
        private static readonly Regex HeadingPattern = new(
            @"^(\d+[\.\)]\s|[IVXLC]+[\.\)]\s|Chương\s|Điều\s|Mục\s|Phần\s)",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        public List<ChunkResult> ChunkPages(List<PageContent> pages)
        {
            var chunks = new List<ChunkResult>();

            foreach (var page in pages)
            {
                var lines = page.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var currentHeading = string.Empty;
                var currentContent = new StringBuilder();

                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // Phát hiện heading
                    if (IsHeading(line))
                    {
                        // Lưu chunk trước đó nếu có
                        if (currentContent.Length >= MinChunkSize)
                        {
                            AddChunks(chunks, currentContent.ToString(), currentHeading, page.PageNumber);
                        }
                        currentHeading = line;
                        currentContent.Clear();
                    }
                    else
                    {
                        currentContent.AppendLine(line);

                        // Nếu content quá lớn, cắt chunk
                        if (currentContent.Length >= MaxChunkSize)
                        {
                            AddChunks(chunks, currentContent.ToString(), currentHeading, page.PageNumber);
                            currentContent.Clear();
                        }
                    }
                }

                // Xử lý content còn lại
                if (currentContent.Length >= MinChunkSize)
                {
                    AddChunks(chunks, currentContent.ToString(), currentHeading, page.PageNumber);
                }
                else if (currentContent.Length > 0 && chunks.Count > 0)
                {
                    // Nối vào chunk cuối nếu quá nhỏ
                    chunks[^1].Content += "\n" + currentContent.ToString().Trim();
                }
                else if (currentContent.Length > 0)
                {
                    chunks.Add(new ChunkResult
                    {
                        Content = currentContent.ToString().Trim(),
                        Heading = currentHeading,
                        PageNumber = page.PageNumber
                    });
                }
            }

            return chunks;
        }

        private bool IsHeading(string line)
        {
            // Dòng ngắn (< 100 ký tự) và khớp pattern heading
            if (line.Length > 100) return false;
            if (HeadingPattern.IsMatch(line)) return true;

            // Dòng ngắn và phần lớn viết hoa
            if (line.Length <= 80)
            {
                var upperCount = line.Count(char.IsUpper);
                var letterCount = line.Count(char.IsLetter);
                if (letterCount > 0 && (double)upperCount / letterCount > 0.6)
                    return true;
            }

            return false;
        }

        private void AddChunks(List<ChunkResult> chunks, string content, string heading, int pageNumber)
        {
            var trimmedContent = content.Trim();
            if (string.IsNullOrWhiteSpace(trimmedContent)) return;

            // Nếu content vẫn quá lớn, chia theo paragraph
            if (trimmedContent.Length > MaxChunkSize)
            {
                var paragraphs = trimmedContent.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                var buffer = new StringBuilder();

                foreach (var para in paragraphs)
                {
                    if (buffer.Length + para.Length > MaxChunkSize && buffer.Length >= MinChunkSize)
                    {
                        chunks.Add(new ChunkResult
                        {
                            Content = buffer.ToString().Trim(),
                            Heading = heading,
                            PageNumber = pageNumber
                        });
                        buffer.Clear();
                    }
                    buffer.AppendLine(para);
                }

                if (buffer.Length > 0)
                {
                    chunks.Add(new ChunkResult
                    {
                        Content = buffer.ToString().Trim(),
                        Heading = heading,
                        PageNumber = pageNumber
                    });
                }
            }
            else
            {
                chunks.Add(new ChunkResult
                {
                    Content = trimmedContent,
                    Heading = heading,
                    PageNumber = pageNumber
                });
            }
        }
    }
}
