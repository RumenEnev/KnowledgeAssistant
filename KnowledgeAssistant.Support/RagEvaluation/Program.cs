using Dapper;
using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Application.Services;
using KnowledgeAssistant.Eval.Core.Judging;
using KnowledgeAssistant.Eval.Infrastructure;
using KnowledgeAssistant.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RagEvaluation.Interfaces;
using RagEvaluation.Services;
using System.Net.Http.Headers;

namespace KnowledgeAssistant.Eval;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        SqlMapper.AddTypeHandler(new VectorTypeHandler());

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var services = new ServiceCollection();
        ConfigureServices(services, config);
        await using var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        var command = args[0].ToLowerInvariant();
        var ct = CancellationToken.None;

        try
        {
            switch (command)
            {
                case "generate-testset":
                    {
                        var perChunk = GetIntArg(args, "--per-chunk", int.Parse(config["Eval:QuestionsPerChunk"] ?? "1"));
                        var generator = sp.GetRequiredService<TestSetGenerationService>();
                        var count = await generator.GenerateAsync(config["Llm:ChatModel"]!, perChunk, ct);
                        Console.WriteLine($"Saved {count} synthetic test queries (one row per chunk x topic).");
                        Console.WriteLine("Hand-review a sample and mix in real user queries before trusting retrieval scores from this set alone.");
                        return 0;
                    }

                case "run-eval":
                    {
                        var runName = GetStringArg(args, "--name") ?? $"run-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
                        var chatModel = GetStringArg(args, "--chat-model") ?? config["Llm:ChatModel"]!;
                        var embeddingModel = GetStringArg(args, "--embedding-model") ?? config["Llm:EmbeddingModel"]!;
                        var judgeModel = GetStringArg(args, "--judge-model") ?? config["Llm:JudgeModel"]!;

                        var evalService = sp.GetRequiredService<EvaluationService>();
                        var progress = new Progress<(int done, int total)>(p =>
                        {
                            if (p.done % 10 == 0 || p.done == p.total)
                                Console.WriteLine($"  {p.done}/{p.total} queries evaluated");
                        });

                        Console.WriteLine($"Running eval '{runName}' (chat={chatModel}, judge={judgeModel})...");
                        var outcome = await evalService.RunEvalAsync(runName, chatModel, embeddingModel, judgeModel, progress, ct);
                        if (outcome.SkippedQueries > 0)
                        {
                            Console.WriteLine($"  ({outcome.SkippedQueries} queries skipped - no candidates or empty budget selection)");
                        }
                        PrintSummary(outcome.Summary);
                        return 0;
                    }

                case "list-runs":
                    {
                        var evalService = sp.GetRequiredService<EvaluationService>();
                        var runs = await evalService.ListRunsAsync(ct);
                        Console.WriteLine($"{"Id",-6}{"Run Name",-30}{"Chat Model",-20}{"Judge Model",-20}Created");
                        foreach (var run in runs)
                        {
                            Console.WriteLine($"{run.Id,-6}{run.RunName,-30}{run.ChatModel,-20}{run.JudgeModel,-20}{run.CreatedAt:yyyy-MM-dd HH:mm}");
                        }
                        return 0;
                    }

                case "show-run":
                    {
                        var runId = int.Parse(GetStringArg(args, "--id") ?? throw new ArgumentException("--id is required"));
                        var evalService = sp.GetRequiredService<EvaluationService>();
                        var summary = await evalService.GetRunSummaryAsync(runId, ct);
                        PrintSummary(summary);
                        return 0;
                    }

                default:
                    PrintUsage();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
        // Some repositories (e.g. ModelRepository) apparently take IConfiguration directly
        // rather than just NpgsqlDataSource - register it so DI can resolve that dependency.
        services.AddSingleton(config);

        // --- Shared with the main app: same connection string, same NpgsqlDataSource pattern ---
        var connString = config.GetConnectionString("KnowledgeAssistant")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:KnowledgeAssistant");

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connString);
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();
        services.AddSingleton(dataSource);

        services.AddScoped<IDocumentRepository, DocumentRepository>();

        // --- Ollama gateways, matching the main app's registrations exactly ---
        var ollamaBaseUrl = config["Llm:OllamaBaseUrl"]!;

        services.AddHttpClient<IModelGateway, OllamaModelGateway>(client =>
        {
            client.BaseAddress = new Uri(ollamaBaseUrl);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        services.AddHttpClient<IModelCatalogGateway, OllamaModelCatalogGateway>(client =>
        {
            client.BaseAddress = new Uri(ollamaBaseUrl);
        });

        // --- DocumentsHandlingService's other dependencies - now wired to match the main
        // app's Program.cs registrations exactly. NOTE: assumes ConfigurationRepository and
        // ModelRepository take a single NpgsqlDataSource constructor param, same as
        // DocumentRepository - adjust here if their real constructors differ. ---
        services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
        services.AddScoped<IModelRepository, ModelRepository>();
        services.AddScoped<ModelCatalogService>();
        services.AddScoped<DocumentsHandlingService>();

        // --- Eval-specific, all new ---
        services.AddScoped<IExperimentRepository, EvalRepository>();
        services.AddScoped<ILlmJudge>(sp => new LlmJudge(sp.GetRequiredService<IModelGateway>()));
        services.AddScoped<TestSetGenerationService>();
        services.AddScoped<EvaluationService>();
    }

    private static void PrintSummary(RagEvaluation.Models.RunSummary summary)
    {
        Console.WriteLine();
        Console.WriteLine($"=== Run: {summary.Run.RunName} (chat: {summary.Run.ChatModel}, judge: {summary.Run.JudgeModel}) ===");
        Console.WriteLine($"Retrieval  - Precision: {summary.MeanPrecisionAtK:F3}  Recall: {summary.MeanRecallAtK:F3}  MRR: {summary.MeanReciprocalRank:F3}  NDCG: {summary.MeanNdcgAtK:F3}");
        Console.WriteLine($"Generation - Faithfulness: {summary.MeanFaithfulness:F2}/5  Relevance: {summary.MeanRelevance:F2}/5  Completeness: {summary.MeanCompleteness:F2}/5");
        Console.WriteLine();
    }

    private static string? GetStringArg(string[] args, string name)
    {
        var idx = Array.IndexOf(args, name);
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }

    private static int GetIntArg(string[] args, string name, int defaultValue)
    {
        var val = GetStringArg(args, name);
        return val is null ? defaultValue : int.Parse(val);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            ka-eval - RAG evaluation tool for KnowledgeAssistant

            Usage:
              ka-eval generate-testset [--per-chunk N]
              ka-eval run-eval [--name RUN_NAME] [--chat-model MODEL] [--embedding-model MODEL] [--judge-model MODEL]
              ka-eval list-runs
              ka-eval show-run --id RUN_ID

            Before first run: apply eval_schema_migration.sql and eval_schema_migration_phase5.sql,
            and fill in appsettings.json (Postgres connection string + Ollama model names).
            """);
    }
}