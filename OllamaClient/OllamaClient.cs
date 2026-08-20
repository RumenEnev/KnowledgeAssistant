using OllamaClients.Configuration;
using System.Text;
using System.Text.Json;

namespace OllamaClients;

public sealed class OllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    public OllamaClient(OllamaConfig config)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(config.BaseUrl),
            Timeout = TimeSpan.FromSeconds(config.RequestTimeoutSeconds)
        };

        _model = config.GenerationModel;
    }

    public async Task<string> GenerateAsync(string instruction, string prompt)
    {
        var requestBody = new 
        {
            model = _model,
            stream = false,
            options = new { temperature = 0 },   // reduces drift into conversational framing
            messages = new object[]
            {
                new { role = "system", content = instruction },
                new { role = "user", content = prompt }
            }
        };

        try
        {
            File.WriteAllText(@"C:\My\DPF and AI\KnowledgeAssistant\src\KnowledgeAssistant\KnowledgeAssistant.Tools\DocumentCreator\bin\Debug\net10.0\Ollama request.txt",
                JsonSerializer.Serialize(requestBody, JsonOptions));
            using var content = new StringContent(JsonSerializer.Serialize(requestBody, JsonOptions), Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("/api/chat", content);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new OllamaCallException($"Ollama returned {(int)response.StatusCode} {response.StatusCode}: {body}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);
            var text = doc.RootElement.GetProperty("message").GetProperty("content").GetString();

            File.WriteAllText(@"C:\My\DPF and AI\KnowledgeAssistant\src\KnowledgeAssistant\KnowledgeAssistant.Tools\DocumentCreator\bin\Debug\net10.0\Ollama response.txt", text);
            if (text is null || string.IsNullOrWhiteSpace(text))
            {
                throw new OllamaCallException("Ollama returned an empty response.");
            }

            return text.Trim();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}