namespace KnowledgeAssistant.Infrastructure;

public sealed class AdessoAiHubOptions
{
    public const string SectionName = "AdessoAiHub";

    public string BaseUrl { get; set; } = "https://adesso-ai-hub.3asabc.de/";

    public string ApiKey { get; set; } = string.Empty;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);
}
