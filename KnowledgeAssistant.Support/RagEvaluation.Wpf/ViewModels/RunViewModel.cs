using CommunityToolkit.Mvvm.ComponentModel;
using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Application.Services;
using KnowledgeAssistant.Domain.Documents;
using Microsoft.Extensions.Logging;
using RagEvaluation.Desktop.Services;
using RagEvaluation.Enums;
using RagEvaluation.Interfaces;
using RagEvaluation.Models;
using RagEvaluation.Services;
using System.Collections.ObjectModel;
using System.Globalization;

namespace RagEvaluation.Desktop.ViewModels;

public sealed partial class RunViewModel : OperationViewModelBase
{
    private const int SmokeSampleSize = 5;

    private static readonly string[] FallbackEmbeddingModels =
    [
        "nomic-embed-text:latest",
        "mxbai-embed-large:latest"
    ];

    private readonly EvaluationService _evaluationService;
    private readonly DocumentsHandlingService _documentsHandlingService;
    private readonly IDocumentRepository _documentRepository;
    private readonly IExperimentRepository _experimentRepository;
    private readonly IConfigurationRepository _configurationRepository;
    private readonly IModelRepository _modelRepository;
    private readonly LlmCatalogService _llm;
    private readonly TestSetHealthService _health;
    private readonly ShellViewModel _shell;
    private readonly ILogger<RunViewModel> _logger;

    private bool _loading;
    private bool _settingsMixed;
    private int _healthVersion;
    private TestSetHealth? _healthResult;

    public RunViewModel(
        EvaluationService evaluationService,
        DocumentsHandlingService documentsHandlingService,
        IDocumentRepository documentRepository,
        IExperimentRepository experimentRepository,
        IConfigurationRepository configurationRepository,
        IModelRepository modelRepository,
        LlmCatalogService llm,
        TestSetHealthService health,
        ShellViewModel shell,
        ILogger<RunViewModel> logger)
        : base(logger)
    {
        _evaluationService = evaluationService;
        _documentsHandlingService = documentsHandlingService;
        _documentRepository = documentRepository;
        _experimentRepository = experimentRepository;
        _configurationRepository = configurationRepository;
        _modelRepository = modelRepository;
        _llm = llm;
        _health = health;
        _shell = shell;
        _logger = logger;
    }

    public ObservableCollection<DocumentChoice> Scopes { get; } = new();
    public ObservableCollection<string> EmbeddingModels { get; } = new();
    public ObservableCollection<string> Providers { get; } = new();
    public ObservableCollection<string> ChatModels { get; } = new();
    public ObservableCollection<string> JudgeModels { get; } = new();
    public ObservableCollection<PreflightItem> Preflight { get; } = new();
    public ObservableCollection<StatusItem> ResultTiles { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFullMode))]
    private bool _isRetrievalOnly = true;

    public bool IsFullMode => !IsRetrievalOnly;

    [ObservableProperty]
    private DocumentChoice? _selectedScope;

    [ObservableProperty]
    private string? _selectedEmbeddingModel;

    [ObservableProperty]
    private string? _selectedChatProvider;

    [ObservableProperty]
    private string? _selectedChatModel;

    [ObservableProperty]
    private string? _selectedJudgeProvider;

    [ObservableProperty]
    private string? _selectedJudgeModel;

    [ObservableProperty]
    private double? _poolSize = 5;

    [ObservableProperty]
    private double? _fanout = 20;

    [ObservableProperty]
    private double? _distanceThreshold = 0.5;

    [ObservableProperty]
    private double? _rrfK = 60;

    [ObservableProperty]
    private string _runName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    private bool _hasPreflightErrors;

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private string _resultTitle = string.Empty;

    [ObservableProperty]
    private string _resultWarning = string.Empty;

    [ObservableProperty]
    private bool _hasResultWarning;

    public bool CanStart => IsIdle && !HasPreflightErrors;

    partial void OnIsRetrievalOnlyChanged(bool value) => RefreshChecklist();

    partial void OnSelectedScopeChanged(DocumentChoice? value)
    {
        if (!_loading)
        {
            _ = RefreshHealthAsync();
        }
    }

    partial void OnSelectedEmbeddingModelChanged(string? value) => RefreshChecklist();

    partial void OnSelectedChatModelChanged(string? value) => RefreshChecklist();

    partial void OnSelectedJudgeModelChanged(string? value) => RefreshChecklist();

    partial void OnSelectedChatProviderChanged(string? value) => _ = LoadModelsAsync(value, ChatModels, isChat: true);

    partial void OnSelectedJudgeProviderChanged(string? value) => _ = LoadModelsAsync(value, JudgeModels, isChat: false);

    protected override void OnBusyChanged() => OnPropertyChanged(nameof(CanStart));

    protected override async Task OnOperationFinishedAsync()
    {
        await _shell.RefreshAsync();
        await RefreshHealthAsync();
    }

    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        _loading = true;
        try
        {
            var available = await _modelRepository.GetEmbeddingModelsAsync(CancellationToken.None);
            Replace(EmbeddingModels, available.Count > 0 ? available : FallbackEmbeddingModels);
            if (SelectedEmbeddingModel is null || !EmbeddingModels.Contains(SelectedEmbeddingModel))
            {
                var settings = await _configurationRepository.GetChunkingSettingsAsync(CancellationToken.None);
                SelectedEmbeddingModel = EmbeddingModels.Contains(settings.EmbeddingModelName)
                    ? settings.EmbeddingModelName
                    : EmbeddingModels.FirstOrDefault();
            }

            if (Providers.Count == 0)
            {
                Replace(Providers, _llm.Providers);
            }

            SelectedChatProvider ??= Providers.FirstOrDefault();
            SelectedJudgeProvider ??= Providers.FirstOrDefault();

            await LoadScopesAsync();
            await LoadSettingsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load the Run page");
            StatusText = $"Error loading the page: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }

        await RefreshHealthAsync();
    }

    private async Task LoadScopesAsync()
    {
        var previousId = SelectedScope?.Id;
        var documents = await _documentRepository.GetAllDocumentsAsync(CancellationToken.None);

        Scopes.Clear();
        Scopes.Add(new DocumentChoice(null, "All documents"));
        foreach (var document in documents.OrderBy(d => d.Title))
        {
            Scopes.Add(new DocumentChoice(document.Id, document.Title));
        }

        SelectedScope = Scopes.FirstOrDefault(s => s.Id == previousId) ?? Scopes[0];
    }

    private async Task LoadSettingsAsync()
    {
        var documents = (await _documentRepository.GetAllDocumentsAsync(CancellationToken.None)).ToList();
        if (documents.Count == 0)
        {
            _settingsMixed = false;
            return;
        }

        var ids = documents.Select(d => d.Id).ToList();
        var saved = await _documentRepository.GetRetrievalConfigsAsync(ids, CancellationToken.None);
        var configs = ids
            .Select(id => saved.TryGetValue(id, out var config) ? config : DocumentRetrievalConfig.Default(id))
            .ToList();

        var first = configs[0];
        PoolSize = first.CandidatePoolSize;
        Fanout = first.CandidateFanout;
        DistanceThreshold = first.MaxDistanceThreshold;
        RrfK = first.RrfK;

        _settingsMixed = configs.Any(c =>
            c.CandidatePoolSize != first.CandidatePoolSize ||
            c.CandidateFanout != first.CandidateFanout ||
            c.RrfK != first.RrfK ||
            Math.Abs(c.MaxDistanceThreshold - first.MaxDistanceThreshold) > 1e-9);
    }

    private async Task LoadModelsAsync(string? provider, ObservableCollection<string> target, bool isChat)
    {
        target.Clear();
        if (string.IsNullOrWhiteSpace(provider))
        {
            return;
        }

        try
        {
            var models = await _llm.GetModelsAsync(provider, CancellationToken.None);
            Replace(target, models);

            var configured = isChat ? _llm.DefaultChatModel : _llm.DefaultJudgeModel;
            var pick = configured is not null && models.Contains(configured) ? configured : models.FirstOrDefault();
            if (isChat)
            {
                SelectedChatModel = pick;
            }
            else
            {
                SelectedJudgeModel = pick;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load models for provider {Provider}", provider);
            StatusText = $"Error loading models for '{provider}': {ex.Message}";
        }
    }

    private static void Replace(ObservableCollection<string> target, IEnumerable<string> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private async Task RefreshHealthAsync()
    {
        var version = ++_healthVersion;
        TestSetHealth? result = null;
        try
        {
            result = await _health.GetAsync(SelectedScope?.Id, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check the test set");
        }

        if (version != _healthVersion)
        {
            return;
        }

        _healthResult = result;
        RefreshChecklist();
    }

    private void RefreshChecklist()
    {
        if (_loading)
        {
            return;
        }

        var items = new List<PreflightItem>();
        if (_healthResult is null)
        {
            items.Add(new PreflightItem(PreflightLevel.Warning, "The test set could not be checked."));
        }
        else if (_healthResult.Questions == 0)
        {
            items.Add(new PreflightItem(PreflightLevel.Error, "No test questions in scope. Generate them on the Test set page."));
        }
        else
        {
            items.Add(new PreflightItem(PreflightLevel.Ok, $"{_healthResult.Questions} test question(s) in scope."));
            if (_healthResult.Orphaned > 0)
            {
                items.Add(new PreflightItem(
                    PreflightLevel.Warning,
                    $"{_healthResult.Orphaned} question(s) are orphaned (their expected chunk no longer exists). They can never be found, so they lower your scores unfairly. Delete them on the Test set page."));
            }
        }

        items.Add(string.IsNullOrWhiteSpace(SelectedEmbeddingModel)
            ? new PreflightItem(PreflightLevel.Error, "Select an embedding model.")
            : new PreflightItem(PreflightLevel.Ok, $"Embedding model: {SelectedEmbeddingModel}. Vectors for it are checked when you start."));

        items.Add(string.IsNullOrWhiteSpace(SelectedChatModel)
            ? new PreflightItem(PreflightLevel.Error, "Select a chat model (it sets the context budget, even for retrieval-only runs).")
            : new PreflightItem(PreflightLevel.Ok, $"Chat model: {SelectedChatModel}."));

        if (IsFullMode)
        {
            items.Add(string.IsNullOrWhiteSpace(SelectedJudgeModel)
                ? new PreflightItem(PreflightLevel.Error, "Select a judge model.")
                : new PreflightItem(PreflightLevel.Ok, $"Judge model: {SelectedJudgeModel}."));
        }

        if (_settingsMixed)
        {
            items.Add(new PreflightItem(
                PreflightLevel.Warning,
                "Documents currently have different saved retrieval settings. Starting the run sets all of them to the values shown here."));
        }

        Preflight.Clear();
        foreach (var item in items)
        {
            Preflight.Add(item);
        }

        HasPreflightErrors = items.Any(i => i.Level == PreflightLevel.Error);
    }

    private string? ValidateSettings()
    {
        if (PoolSize is not >= 1 || Fanout is not >= 1 || RrfK is not >= 1)
        {
            return "Pool size, fan-out and RRF k must be at least 1.";
        }

        if (DistanceThreshold is not > 0)
        {
            return "The distance threshold must be greater than 0.";
        }

        return null;
    }

    public Task StartAsync(Func<string, bool> confirm)
    {
        if (!CanStart)
        {
            return Task.CompletedTask;
        }

        var problem = ValidateSettings();
        if (problem is not null)
        {
            StatusText = problem;
            return Task.CompletedTask;
        }

        var embeddingModel = SelectedEmbeddingModel!;
        var chatProvider = SelectedChatProvider ?? string.Empty;
        var chatModel = SelectedChatModel!;
        var judgeProvider = SelectedJudgeProvider ?? string.Empty;
        var judgeModel = SelectedJudgeModel ?? string.Empty;
        var documentId = SelectedScope?.Id;
        var retrievalOnly = IsRetrievalOnly;
        var runName = string.IsNullOrWhiteSpace(RunName)
            ? BuildRunName(embeddingModel, chatModel, retrievalOnly)
            : RunName.Trim();

        HasResult = false;
        return RunOperationAsync(
            "Applying retrieval settings…",
            async ct =>
            {
                await ApplyRetrievalSettingsAsync(ct);

                StatusText = "Checking that retrieval returns results…";
                var (answered, sampled) = await SmokeTestAsync(chatModel, embeddingModel, documentId, ct);
                if (sampled == 0)
                {
                    throw new InvalidOperationException("The sampled test questions reference topics that no longer exist.");
                }

                if (answered == 0)
                {
                    throw new InvalidOperationException(
                        $"None of {sampled} sample questions returned any chunks with '{embeddingModel}'. " +
                        "Its vectors are probably missing (use Vectors on the Documents page), or the embedding server is not reachable.");
                }

                if (answered < sampled && !confirm(
                        $"{sampled - answered} of {sampled} sample questions returned no chunks with '{embeddingModel}'. " +
                        "Some documents may lack vectors for it. Those questions are skipped and NOT counted in the averages, which makes the scores look better than they are.\n\n" +
                        "Start the run anyway?"))
                {
                    throw new OperationCanceledException();
                }

                var chatGateway = retrievalOnly ? null : _llm.GetGateway(chatProvider);
                var judgeGateway = retrievalOnly ? null : _llm.GetGateway(judgeProvider);

                var progress = new Progress<EvalProgress>(p =>
                {
                    ProgressMax = Math.Max(p.Total, 1);
                    ProgressValue = p.Done;
                    StatusText = $"{p.Done}/{p.Total} · {PhaseLabel(p.Phase)}: {Shorten(p.QueryText)}";
                });

                EvalRunOutcome outcome;
                try
                {
                    outcome = await _evaluationService.RunEvalAsync(
                        chatGateway, chatProvider, judgeGateway, judgeProvider,
                        runName, chatModel, embeddingModel, judgeModel,
                        documentId, progress, ct, retrievalOnly);
                }
                catch (OperationCanceledException)
                {
                    return "Cancelled. Any questions processed so far were kept as a partial run. " +
                           "Don't compare it with complete runs.";
                }

                ShowResult(outcome, full: !retrievalOnly);
                var processed = outcome.TotalQueries - outcome.SkippedQueries;
                return $"Run '{runName}' finished: {processed} of {outcome.TotalQueries} question(s) fully processed.";
            },
            cancelledText: "Run not started.");
    }

    private async Task ApplyRetrievalSettingsAsync(CancellationToken ct)
    {
        var pool = (int)PoolSize!.Value;
        var fanout = (int)Fanout!.Value;
        var rrfK = (int)RrfK!.Value;
        var threshold = DistanceThreshold!.Value;
        var ids = (await _documentRepository.GetAllDocumentsAsync(ct)).Select(d => d.Id).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var saved = await _documentRepository.GetRetrievalConfigsAsync(ids, ct);
        foreach (var id in ids)
        {
            var existing = saved.TryGetValue(id, out var config) ? config : DocumentRetrievalConfig.Default(id);
            var same = existing.CandidatePoolSize == pool
                       && existing.CandidateFanout == fanout
                       && existing.RrfK == rrfK
                       && Math.Abs(existing.MaxDistanceThreshold - threshold) < 1e-9;
            if (same)
            {
                continue;
            }

            await _documentRepository.SaveRetrievalConfigAsync(new DocumentRetrievalConfig
            {
                DocumentId = id,
                ChunkSize = existing.ChunkSize,
                ChunkOverlap = existing.ChunkOverlap,
                CandidatePoolSize = pool,
                CandidateFanout = fanout,
                MaxDistanceThreshold = threshold,
                RrfK = rrfK,
                TargetInjectionFraction = existing.TargetInjectionFraction,
                MaxInjectionFraction = existing.MaxInjectionFraction
            }, ct);
        }

        _settingsMixed = false;
    }

    private async Task<(int Answered, int Sampled)> SmokeTestAsync(
        string chatModel, string embeddingModel, int? documentId, CancellationToken ct)
    {
        var queries = (await _experimentRepository.LoadTestQueriesAsync(documentId, ct)).ToList();
        if (queries.Count == 0)
        {
            return (0, 0);
        }

        var topicNameById = (await _documentRepository.GetAllTopicsAsync(ct)).ToDictionary(t => t.Id, t => t.Name);
        var step = Math.Max(1, queries.Count / SmokeSampleSize);
        var sample = queries.Where((_, index) => index % step == 0).Take(SmokeSampleSize).ToList();
        var answered = 0;
        var sampled = 0;
        foreach (var query in sample)
        {
            ct.ThrowIfCancellationRequested();
            if (!topicNameById.TryGetValue(query.TopicId, out var topicName))
            {
                continue;
            }

            sampled++;
            var selection = await _documentsHandlingService.SelectRelevantChunksForModelAsync(
                chatModel, embeddingModel, topicName, query.QueryText, ct);
            if (selection is not null && selection.Chunks.Count > 0)
            {
                answered++;
            }
        }

        return (answered, sampled);
    }

    private void ShowResult(EvalRunOutcome outcome, bool full)
    {
        var summary = outcome.Summary;
        var scored = outcome.TotalQueries - outcome.TopicMissing - outcome.NoCandidates;

        ResultTiles.Clear();
        ResultTiles.Add(new StatusItem("Recall", F3(summary.MeanRecallAtK), "Mean recall@k over the scored questions"));
        ResultTiles.Add(new StatusItem("Precision", F3(summary.MeanPrecisionAtK), "Mean precision@k over the scored questions"));
        ResultTiles.Add(new StatusItem("MRR", F3(summary.MeanReciprocalRank), "Mean reciprocal rank over the scored questions"));
        ResultTiles.Add(new StatusItem("NDCG", F3(summary.MeanNdcgAtK), "Mean NDCG@k over the scored questions"));
        if (full)
        {
            ResultTiles.Add(new StatusItem("Faithfulness", $"{F2(summary.MeanFaithfulness)}/5"));
            ResultTiles.Add(new StatusItem("Relevance", $"{F2(summary.MeanRelevance)}/5"));
            ResultTiles.Add(new StatusItem("Completeness", $"{F2(summary.MeanCompleteness)}/5"));
        }

        ResultTiles.Add(new StatusItem(
            "Questions scored",
            $"{scored}/{outcome.TotalQueries}",
            "Retrieval metrics are averaged over these only"));

        var warnings = new List<string>();
        if (outcome.NoCandidates > 0)
        {
            warnings.Add($"{outcome.NoCandidates} question(s) returned no candidates and are NOT in the averages, so recall is optimistic. Usually the embedding model has no vectors for those documents.");
        }

        if (full && outcome.NothingIncluded > 0)
        {
            warnings.Add($"{outcome.NothingIncluded} question(s) had no chunk within the distance threshold or context budget, so they have retrieval scores but no answer scores. Try a larger threshold.");
        }

        if (outcome.TopicMissing > 0)
        {
            warnings.Add($"{outcome.TopicMissing} question(s) belong to a topic that no longer exists.");
        }

        if (outcome.Failed > 0)
        {
            warnings.Add($"{outcome.Failed} question(s) failed with an error; see the log.");
        }

        ResultTitle = $"{summary.Run.RunName}  ({(full ? "full run" : "retrieval only")})";
        ResultWarning = string.Join("\n", warnings);
        HasResultWarning = warnings.Count > 0;
        HasResult = true;
    }

    private string BuildRunName(string embeddingModel, string chatModel, bool retrievalOnly)
    {
        var embedding = embeddingModel.Split(':')[0];
        var mode = retrievalOnly ? "retr" : chatModel.Split(':')[0];
        var threshold = DistanceThreshold!.Value.ToString("0.##", CultureInfo.InvariantCulture);
        return $"{embedding} {mode} pool{(int)PoolSize!.Value} thr{threshold} rrf{(int)RrfK!.Value} {DateTime.Now:MM-dd HH:mm}";
    }

    private static string PhaseLabel(EvalPhase phase) => phase switch
    {
        EvalPhase.Retrieval => "Retrieving",
        EvalPhase.Generation => "Answering",
        EvalPhase.Judging => "Judging",
        EvalPhase.Skipped => "Skipped",
        EvalPhase.Failed => "Failed",
        _ => phase.ToString()
    };

    private static string Shorten(string text) => text.Length <= 90 ? text : text[..90] + "…";

    private static string F3(double value) => value.ToString("F3", CultureInfo.InvariantCulture);

    private static string F2(double value) => value.ToString("F2", CultureInfo.InvariantCulture);
}