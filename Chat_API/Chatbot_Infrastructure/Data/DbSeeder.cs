using Chatbot_Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Chatbot_Infrastructure.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

            try
            {
                logger.LogInformation("Checking and applying database migrations...");
                await context.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to apply database migrations.");
                return;
            }

            var configuredPath = config["SeedSettings:PdfDirectoryPath"];
            var pdfPath = configuredPath;
            if (!string.IsNullOrWhiteSpace(configuredPath) && !Path.IsPathRooted(configuredPath))
            {
                pdfPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configuredPath));
            }

            if (string.IsNullOrWhiteSpace(pdfPath))
            {
                logger.LogWarning("SeedSettings:PdfDirectoryPath is empty. Skip seeding.");
                return;
            }

            var pdfFiles = new List<string>();
            if (File.Exists(pdfPath))
            {
                if (!Path.GetExtension(pdfPath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogWarning("Configured path is a file but not PDF: {PdfPath}", pdfPath);
                    return;
                }

                pdfFiles.Add(pdfPath);
            }
            else if (Directory.Exists(pdfPath))
            {
                pdfFiles.AddRange(Directory.GetFiles(pdfPath, "*.pdf"));
            }
            else
            {
                logger.LogWarning("Configured seed path does not exist: {PdfPath}", pdfPath);
                return;
            }

            if (pdfFiles.Count == 0)
            {
                logger.LogInformation("No PDF files found for seeding at path: {PdfPath}", pdfPath);
                return;
            }

            var geminiApiKey = config["AiSettings:ApiKey"];
            if (string.IsNullOrWhiteSpace(geminiApiKey))
            {
                logger.LogWarning("AiSettings:ApiKey is missing. Skip seeding to avoid startup failure.");
                return;
            }

            var documentProcessor = scope.ServiceProvider.GetRequiredService<IDocumentProcessingService>();
            logger.LogInformation("Found {Count} PDF files. Starting seed ingest...", pdfFiles.Count);

            foreach (var filePath in pdfFiles)
            {
                var fileName = Path.GetFileName(filePath);
                var existingDoc = await context.Documents.FirstOrDefaultAsync(d => d.FileName == fileName);

                if (existingDoc != null)
                {
                    var chunkCount = await context.DocumentChunks.CountAsync(c => c.DocumentId == existingDoc.Id);
                    if (chunkCount > 0)
                    {
                        logger.LogInformation("Skip seeded file {FileName} ({ChunkCount} chunks already exists).", fileName, chunkCount);
                        continue;
                    }

                    context.Documents.Remove(existingDoc);
                    await context.SaveChangesAsync();
                }

                try
                {
                    var response = await documentProcessor.ProcessDocumentAsync(filePath, fileName);
                    logger.LogInformation("Seeded file {FileName}: {ChunkCount} chunks.", fileName, response.ChunkCount);

                    // Delay between files to avoid rate limit when seeding multiple PDFs
                    await Task.Delay(3000);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to seed file {FileName}", fileName);
                }
            }

            logger.LogInformation("Seeding completed.");
        }
    }
}
