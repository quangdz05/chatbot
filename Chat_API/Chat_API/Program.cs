using System.Text;
using System.Threading.RateLimiting;
using Chat_API.Background;
using Chat_API.Middleware;
using Chatbot_Application.Interfaces;
using Chatbot_Application.Interfaces.Repositories;
using Chatbot_Application.Services;
using Chatbot_Infrastructure.Data;
using Chatbot_Infrastructure.Repositories;
using Chatbot_Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

        return new BadRequestObjectResult(new
        {
            error = "validation_error",
            message = "Validation failed.",
            details = errors
        });
    };
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), o => o.UseVector()));

builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IDocumentChunkRepository, DocumentChunkRepository>();
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();

builder.Services.AddHttpClient<IEmbeddingService, GeminiEmbeddingService>();
builder.Services.AddHttpClient<ILlmService, GeminiLlmService>();

builder.Services.AddScoped<IPdfParserService, PdfParserService>();
builder.Services.AddScoped<IChunkingService, SemanticChunkingService>();
builder.Services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
builder.Services.AddScoped<ISemanticSearchService, SemanticSearchService>();
builder.Services.AddScoped<IChatbotService, ChatbotService>();

builder.Services.AddSingleton<IDocumentIngestionQueue, DocumentIngestionQueue>();
builder.Services.AddHostedService<DocumentIngestionWorker>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ConfiguredOrigins", policy =>
    {
        var origins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        var allowCloudflareTunnel = builder.Configuration.GetValue<bool?>("CorsSettings:AllowCloudflareTunnel") ?? false;
        var explicitOrigins = origins
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .ToArray();

        if (explicitOrigins.Length > 0)
        {
            policy.WithOrigins(explicitOrigins);
        }

        if (allowCloudflareTunnel)
        {
            policy.SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrWhiteSpace(origin))
                {
                    return false;
                }

                if (explicitOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }

                return Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                    && uri.Host.EndsWith(".trycloudflare.com", StringComparison.OrdinalIgnoreCase);
            });
        }

        policy
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtKey = builder.Configuration["AuthSettings:JwtKey"];
        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            throw new InvalidOperationException("AuthSettings:JwtKey must be configured via environment variable or user secrets.");
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["AuthSettings:Issuer"] ?? "ChatApi",
            ValidAudience = builder.Configuration["AuthSettings:Audience"] ?? "ChatApiClient",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("api", limiter =>
    {
        limiter.PermitLimit = builder.Configuration.GetValue<int?>("RateLimitSettings:PermitLimit") ?? 60;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 10;
    });
});

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestPath | HttpLoggingFields.ResponseStatusCode;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseHttpLogging();
app.UseRateLimiter();
app.UseCors("ConfiguredOrigins");

var forceHttps = builder.Configuration.GetValue<bool?>("Network:ForceHttpsRedirection") ?? !app.Environment.IsDevelopment();
if (forceHttps)
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapControllers().RequireRateLimiting("api");

var seedEnabled = builder.Configuration.GetValue<bool?>("SeedSettings:Enabled") ?? false;
if (seedEnabled)
{
    await DbSeeder.SeedAsync(app.Services);
}

app.Run();
