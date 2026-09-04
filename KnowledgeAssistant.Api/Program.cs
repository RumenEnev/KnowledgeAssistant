using Dapper;
using KnowledgeAssistant.Api.ErrorHandling;
using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Application.Services;
using KnowledgeAssistant.Infrastructure;
using KnowledgeAssistant.Infrastructure.Streaming;
using KnowledgeAssistant.Infrastructure.ToolCallRegistry;
using Npgsql;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// MVC and error handling
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Application services
builder.Services.AddScoped<ConversationService>();
builder.Services.AddScoped<ModelCatalogService>();
builder.Services.AddScoped<DocumentsHandlingService>();

// Repositories
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
builder.Services.AddScoped<IModelRepository, ModelRepository>();
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IToolRepository, ToolRepository>();

// Model-provider configuration
var ollamaBaseUrl = builder.Configuration["Ollama:BaseUrl"];
if (string.IsNullOrWhiteSpace(ollamaBaseUrl))
{
    throw new InvalidOperationException("Configuration value 'Ollama:BaseUrl' is required.");
}

var aiHubBaseUrl = builder.Configuration["AdessoAiHub:BaseUrl"];
if (string.IsNullOrWhiteSpace(aiHubBaseUrl))
{
    throw new InvalidOperationException("Configuration value 'AdessoAiHub:BaseUrl' is required.");
}

var aiHubApiKey = builder.Configuration["AdessoAiHub:ApiKey"];
if (string.IsNullOrWhiteSpace(aiHubApiKey))
{
    throw new InvalidOperationException("Configuration value 'AdessoAiHub:ApiKey' is required.");
}

builder.Services.Configure<AdessoAiHubOptions>(builder.Configuration.GetSection("AdessoAiHub"));
builder.Services.AddHttpClient<OllamaModelGateway>(client =>
{
    client.BaseAddress = new Uri(EnsureTrailingSlash(ollamaBaseUrl));
});

builder.Services.AddHttpClient<AdessoAiHubModelGateway>(client =>
{
    client.BaseAddress = new Uri(EnsureTrailingSlash(aiHubBaseUrl));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aiHubApiKey);
});

builder.Services.AddHttpClient<OllamaModelCatalogGateway>(client =>
{
    client.BaseAddress = new Uri(EnsureTrailingSlash(ollamaBaseUrl));
});

builder.Services.AddHttpClient<AdessoAiHubModelCatalogGateway>(client =>
{
    client.BaseAddress = new Uri(EnsureTrailingSlash(aiHubBaseUrl));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aiHubApiKey);
});

builder.Services.AddTransient<INamedModelCatalogGateway>(serviceProvider => serviceProvider.GetRequiredService<OllamaModelCatalogGateway>());
builder.Services.AddTransient<INamedModelCatalogGateway>(serviceProvider => serviceProvider.GetRequiredService<AdessoAiHubModelCatalogGateway>());
builder.Services.AddScoped<IModelProviderRegistry, ModelProviderRegistry>();

builder.Services.AddTransient<IModelGateway>(serviceProvider => serviceProvider.GetRequiredService<OllamaModelGateway>());
builder.Services.AddTransient<INamedModelGateway>(serviceProvider => serviceProvider.GetRequiredService<OllamaModelGateway>());
builder.Services.AddTransient<INamedModelGateway>(serviceProvider => serviceProvider.GetRequiredService<AdessoAiHubModelGateway>());
builder.Services.AddScoped<IModelGatewayResolver, ModelGatewayResolver>();

// Tool calling and streaming
builder.Services.AddSingleton<IPendingToolCallRegistry, PendingToolCallRegistry>();

// One instance per HTTP request.
builder.Services.AddScoped<SseWriterAccessor>();

// Executes locally registered tools.
builder.Services.AddScoped<IToolExecutor, LocalToolExecutor>();
builder.Services.AddHttpClient<IToolExecutionService, ToolExecutionService>();

// PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("KnowledgeAssistant")
    ?? throw new InvalidOperationException("Connection string 'KnowledgeAssistant' is missing.");

var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.UseVector();
var dataSource = dataSourceBuilder.Build();
builder.Services.AddSingleton(dataSource);

// CORS
var allowedOriginPatterns = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                                ?? throw new InvalidOperationException(
                                    "Configuration value 'Cors:AllowedOrigins' is missing.");

var allowedOriginRegexes = allowedOriginPatterns
    .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
    .Select(pattern => new Regex(
        "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant |
        RegexOptions.Compiled))
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
                allowedOriginRegexes.Any(regex =>
                    regex.IsMatch(origin)))
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();
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

static string EnsureTrailingSlash(string value)
{
    return value.EndsWith("/", StringComparison.Ordinal)
        ? value
        : value + "/";
}