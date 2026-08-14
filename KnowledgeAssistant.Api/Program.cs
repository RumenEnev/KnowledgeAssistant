using Dapper;
using KnowledgeAssistant.Api.ErrorHandling;
using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Application.Services;
using KnowledgeAssistant.Infrastructure;
using KnowledgeAssistant.Infrastructure.Streaming;
using KnowledgeAssistant.Infrastructure.ToolCallRegistry;
using Npgsql;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddScoped<ConversationService>();
builder.Services.AddScoped<ModelCatalogService>();
builder.Services.AddScoped<DocumentsHandlingService>();
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
builder.Services.AddScoped<IModelRepository, ModelRepository>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IToolRepository, ToolRepository>();

var ollamaBaseUrl = builder.Configuration["Ollama:BaseUrl"]
    ?? throw new InvalidOperationException("Configuration value 'Ollama:BaseUrl' is missing.");

builder.Services.AddHttpClient<IModelGateway, OllamaModelGateway>(client =>
{
    client.BaseAddress = new Uri(ollamaBaseUrl);
});

builder.Services.AddHttpClient<IModelCatalogGateway, OllamaModelCatalogGateway>(client =>
{
    client.BaseAddress = new Uri(ollamaBaseUrl);
});

// No fixed base address: each tool's endpoint_url is a full URL stored in the database.
builder.Services.AddSingleton<IPendingToolCallRegistry, PendingToolCallRegistry>();  // must outlive any single request
builder.Services.AddScoped<SseWriterAccessor>();                                     // one per request
builder.Services.AddScoped<IToolExecutor, LocalToolExecutor>();                      // was: HttpToolExecutor

var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("KnowledgeAssistant"));
dataSourceBuilder.UseVector();
var dataSource = dataSourceBuilder.Build();
builder.Services.AddSingleton(dataSource);

builder.Services.AddSingleton(dataSource);

var allowedOriginPatterns = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? throw new InvalidOperationException("Configuration value 'Cors:AllowedOrigins' is missing.");

// Patterns support '*' wildcards (e.g. "http://*:4200") so the API can be called
// from any host on the network, not just localhost.
var allowedOriginRegexes = allowedOriginPatterns
    .Select(pattern => new Regex(
        "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$",
        RegexOptions.IgnoreCase))
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
    {
        policy.SetIsOriginAllowed(origin => allowedOriginRegexes.Any(regex => regex.IsMatch(origin)))
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAngularDev");

app.UseAuthorization();
app.MapControllers();

SqlMapper.AddTypeHandler(new VectorTypeHandler());
app.Run();