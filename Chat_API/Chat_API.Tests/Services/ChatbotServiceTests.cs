using Chatbot_Application.DTOs;
using Chatbot_Application.Interfaces;
using Chatbot_Application.Interfaces.Repositories;
using Chatbot_Application.Services;
using Chatbot_Domain.Entities;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Chat_API.Tests.Services;

public class ChatbotServiceTests
{
    private readonly Mock<ISemanticSearchService> _search = new();
    private readonly Mock<ILlmService> _llm = new();
    private readonly Mock<IConversationRepository> _conversation = new();
    private readonly IConfiguration _config;

    public ChatbotServiceTests()
    {
        _config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Support:Hotline"] = "1900-1111",
            ["Rag:NoContextFallback"] = "Thong tin nay hien chua co trong tai lieu ho tro. Vui long lien he hotline: [SO HOTLINE] de duoc ho tro them.",
            ["Rag:MinSimilarity"] = "0.7",
            ["Rag:TopK"] = "3"
        }).Build();

        _conversation.Setup(c => c.GetOrCreateAsync(It.IsAny<Guid?>()))
            .ReturnsAsync(new Conversation { Id = Guid.NewGuid() });
    }

    [Fact]
    public async Task OutOfScope_ShouldReturnFallbackHotline()
    {
        _search.Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>()))
            .ReturnsAsync(Array.Empty<RetrievedChunk>());

        var service = new ChatbotService(_search.Object, _llm.Object, _conversation.Object, _config);
        var result = await service.GetResponseAsync(new ChatRequest { Message = "ngoai pham vi" });

        Assert.Contains("1900-1111", result.Answer);
        Assert.False(result.IsFromKnowledgeBase);
    }

    [Fact]
    public async Task NoContext_ShouldNotCallLlm()
    {
        _search.Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>()))
            .ReturnsAsync(Array.Empty<RetrievedChunk>());

        var service = new ChatbotService(_search.Object, _llm.Object, _conversation.Object, _config);
        _ = await service.GetResponseAsync(new ChatRequest { Message = "abc" });

        _llm.Verify(l => l.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatHistoryMessage>>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LowSimilarity_ShouldReturnFallback()
    {
        _search.Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<double>()))
            .ReturnsAsync(new[] { new RetrievedChunk { Similarity = 0.4, Content = "x" } });

        var service = new ChatbotService(_search.Object, _llm.Object, _conversation.Object, _config);
        var result = await service.GetResponseAsync(new ChatRequest { Message = "abc" });

        Assert.False(result.IsFromKnowledgeBase);
        _llm.Verify(l => l.GenerateResponseAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<ChatHistoryMessage>>(), It.IsAny<string>()), Times.Never);
    }
}
