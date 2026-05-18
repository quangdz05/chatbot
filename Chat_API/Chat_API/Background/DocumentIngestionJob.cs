namespace Chat_API.Background
{
    public record DocumentIngestionJob(Guid DocumentId, string FilePath, string FileName);
}
