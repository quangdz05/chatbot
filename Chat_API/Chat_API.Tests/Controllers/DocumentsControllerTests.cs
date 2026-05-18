using Chat_API.Background;
using Chat_API.Controllers;
using Chatbot_Application.Interfaces.Repositories;
using Chatbot_Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Chat_API.Tests.Controllers;

public class DocumentsControllerTests
{
    [Fact]
    public void Upload_ShouldRequireAdminRole()
    {
        var attr = typeof(DocumentsController).GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(attr);
        Assert.Equal("Admin", attr!.Roles);
    }

    [Fact]
    public async Task Delete_ShouldDeleteDocumentAndChunks()
    {
        var documentId = Guid.NewGuid();
        var docRepo = new Mock<IDocumentRepository>();
        var chunkRepo = new Mock<IDocumentChunkRepository>();
        var queue = new Mock<IDocumentIngestionQueue>();
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();

        docRepo.Setup(x => x.GetByIdAsync(documentId)).ReturnsAsync(new Document
        {
            Id = documentId,
            FileName = "a.pdf",
            FilePath = string.Empty,
            Status = DocumentStatus.Completed
        });

        var controller = new DocumentsController(docRepo.Object, chunkRepo.Object, queue.Object, config);
        var result = await controller.Delete(documentId);

        Assert.IsType<NoContentResult>(result);
        chunkRepo.Verify(x => x.DeleteByDocumentIdAsync(documentId), Times.Once);
        docRepo.Verify(x => x.DeleteAsync(documentId), Times.Once);
    }
}
