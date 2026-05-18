using Chatbot_Application.DTOs;
using Chatbot_Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chat_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class ChatController : ControllerBase
    {
        private readonly IChatbotService _chatbotService;

        public ChatController(IChatbotService chatbotService)
        {
            _chatbotService = chatbotService;
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { Error = "Message cannot be empty." });
            }

            var response = await _chatbotService.GetResponseAsync(request);
            return Ok(response);
        }

        [HttpPost("embed/ask")]
        public async Task<IActionResult> AskForEmbed([FromBody] ChatRequest request)
        {
            var response = await _chatbotService.GetResponseAsync(request);
            return Ok(response);
        }
    }
}
