using Chatbot_Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Chat_API.Background
{
    public class DocumentIngestionWorker : BackgroundService
    {
        private readonly IDocumentIngestionQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DocumentIngestionWorker> _logger;

        public DocumentIngestionWorker(
            IDocumentIngestionQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<DocumentIngestionWorker> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var job = await _queue.DequeueAsync(stoppingToken);

                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IDocumentProcessingService>();

                try
                {
                    _logger.LogInformation("Processing document job {DocumentId}", job.DocumentId);
                    await processor.ProcessExistingDocumentAsync(job.DocumentId, job.FilePath, job.FileName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed processing job {DocumentId}", job.DocumentId);
                }
            }
        }
    }
}
