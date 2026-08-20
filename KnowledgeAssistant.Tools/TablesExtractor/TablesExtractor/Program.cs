using KnowledgeAssistant.Contracts.Dto;
using Microsoft.Extensions.Configuration;
using OllamaClients;
using OllamaClients.Configuration;
using System.Text.Json;
using TableExtraction;
using TablesExtractor.Models;

internal class Program
{
    private static AppConfig? config;

    private async static Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("At least one argument is required: the URL to extract tables from.");
            return;
        }

        LoadConfiguration();
        string url = args[0];
        var ollamaClient = new OllamaClient(config.Ollama);
        var extractor = new TableExtractor();
        List<string> tables = await extractor.ExtractTablesAsync(url);
        for (int i = 0; i < tables.Count; i++)
        {
            Console.WriteLine($"--- Parsing table {i + 1} (length {tables[i].Length} chars) ---");
            var result = await ollamaClient.GenerateAsync(Instructions.TableToJsonInstruction(), tables[i]);
            Console.WriteLine(result);
        }
    }

    private static void LoadConfiguration()
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddEnvironmentVariables(prefix: "DOCCREATOR_")
                .Build();

            config = configuration.Get<AppConfig>() ?? new AppConfig();
            if (string.IsNullOrWhiteSpace(config.Database.ConnectionString))
            {
                throw new InvalidOperationException("Database connection string is not configured.");
            }
        }
        catch (Exception ex)
        {
            WriteResultAndExit(
                ToolResult.Error("configuration_error", $"Failed to load configuration: {ex.Message}"),
                ExitCode.ConfigurationError,
                jsonOptions);
            return;
        }
    }

    private static void WriteResultAndExit(ToolResult result, int exitCode, JsonSerializerOptions options)
    {
        Console.WriteLine(JsonSerializer.Serialize(result, options));
        Thread.Sleep(5000);
        Environment.Exit(exitCode);
    }
}