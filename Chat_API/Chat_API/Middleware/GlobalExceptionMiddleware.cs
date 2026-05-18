using System.Net;
using System.Text.Json;

namespace Chat_API.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IHostEnvironment _environment;

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger,
            IHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception. CorrelationId: {CorrelationId}", context.Items[CorrelationIdMiddleware.HeaderName]);
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json";

                var payload = new
                {
                    error = "internal_server_error",
                    message = "An unexpected error occurred.",
                    correlationId = context.Items[CorrelationIdMiddleware.HeaderName]?.ToString(),
                    detail = _environment.IsDevelopment()
                        ? new
                        {
                            exception = ex.GetType().FullName,
                            ex.Message
                        }
                        : null
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
            }
        }
    }
}
