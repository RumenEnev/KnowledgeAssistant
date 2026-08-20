using DocumentCreator.Configuration;
using DocumentCreator.Models;
using DocumentCreator.Models.Ollama;
using OllamaClients;
using DocumentCreator.Services.CSharp;
using KnowledgeAssistant.Contracts.Dto;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

internal class Program
{
    private static AppConfig? config;

    private static async Task Main(string[] args)
    {
        LoadConfiguration();
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // --- Parse arguments ---
        if (args.Length == 0)
        {
            WriteResultAndExit(ToolResult.Error("invalid_arguments", "Usage: DocumentCreator.exe <fileName> [--repo <repositoryHint>]"),
                ExitCode.GeneralError,
                jsonOptions);
            return;
        }

        string output;
        var folders = JsonSerializer.Deserialize<string[]>(args[0]);
        var fileName = Path.GetFileName(folders[0]);

        // Sanitize the file name to prevent directory traversal and invalid characters
        if (Regex.IsMatch(fileName, @"\.\.(?:/|\\)"))
        {
            output = JsonSerializer.Serialize(new ToolResult()
            {
                Status = "error",
                OutputPath = null,
                Message = "Invalid file path.",
                Reason = "invalid_file_path"
            });

            Console.WriteLine(output);
            Environment.Exit(1);
        }

        fileName = Regex.Replace(fileName, @"[^a-zA-Z0-9_.-]", "");
        folders = folders.Skip(1).ToArray();
        if (folders?.Any() == true)
        {
            var files = FindFiles(fileName, folders);
            var fileContent = File.ReadAllText(files.FirstOrDefault() ?? string.Empty);
            var types = TypeExtractor.ExtractTypes(fileContent);
            var ollamaClient = new OllamaClient(config.Ollama);
            var document = new StringBuilder();
            foreach (var type in types)
            {
                var memberSnippets = type.Properties
                            .Select(p => (Name: p.Name, Snippet: p.ToString()))
                            .Concat(type.Methods.Select(m => (Name: m.Name, Snippet: m.ToString())))
                            .ToList();

                if (memberSnippets.Count <= 8)
                {
                    var wholeTypeSource = type + "\n\n" + string.Join("\n\n", memberSnippets.Select(m => m.Snippet));

                    var instruction = Instructions.SingleShotInstruction();
                    var markdown = await ollamaClient.GenerateAsync(instruction, fileContent);
                    document.AppendLine(markdown.Trim());
                }
                else
                {
                    var batches = memberSnippets.Chunk(8);
                    foreach (var batch in batches)
                    {
                        var batchSource = string.Join("\n\n", batch.Select(m => m.Snippet));
                        var batchMarkdown = await ollamaClient.GenerateAsync(Instructions.BatchInstruction(batchSource), batchSource);
                        document.AppendLine(batchMarkdown.Trim());
                        document.AppendLine();
                    }
                }

                fileName = Path.GetFileNameWithoutExtension(fileName);
                File.WriteAllText($"{config.Output.Directory}\\{fileName}.md", document.ToString().Trim());
            }

            output = JsonSerializer.Serialize(new ToolResult()
            {
                Status = "success",
                OutputPath = $"{config.Output.Directory}\\{fileName}.md",
                Message = "Document created successfully.",
                Reason = null
            });
        }
        else
        {
            output = JsonSerializer.Serialize(new ToolResult()
            {
                Status = "error",
                OutputPath = null,
                Message = "No files found matching the specified name.",
                Reason = "file_not_found"
            });
        }

        Console.WriteLine(output);
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

    private static IEnumerable<string> FindFiles(string fileName, IEnumerable<string> folders)
    {
        var results = new ConcurrentBag<string>();
        Parallel.ForEach(folders, folder =>
        {
            if (!Directory.Exists(folder))
            {
                return;
            }

            try
            {
                var allFiles = Directory.EnumerateFiles(folder, "*", System.IO.SearchOption.AllDirectories);
                foreach (var file in allFiles)
                {
                    if (string.Equals(Path.GetFileName(file), fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(file);
                    }
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (PathTooLongException) { }
        });

        return results;
    }
}