using System.Threading.Channels;

namespace Chat_API.Background
{
    public interface IDocumentIngestionQueue
    {
        ValueTask QueueAsync(DocumentIngestionJob job, CancellationToken cancellationToken);
        ValueTask<DocumentIngestionJob> DequeueAsync(CancellationToken cancellationToken);
    }

    public class DocumentIngestionQueue : IDocumentIngestionQueue
    {
        private readonly Channel<DocumentIngestionJob> _queue = Channel.CreateUnbounded<DocumentIngestionJob>();

        public ValueTask QueueAsync(DocumentIngestionJob job, CancellationToken cancellationToken)
            => _queue.Writer.WriteAsync(job, cancellationToken);

        public ValueTask<DocumentIngestionJob> DequeueAsync(CancellationToken cancellationToken)
            => _queue.Reader.ReadAsync(cancellationToken);
    }
}
