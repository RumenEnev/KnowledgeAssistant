using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnowledgeAssistant.Application.Abstraction;
using KnowledgeAssistant.Application.Services;
using KnowledgeAssistant.Contracts.Enums;
using Microsoft.Extensions.Logging;
using RagEvaluation.Desktop.Models;
using RagEvaluation.Interfaces;
using RagEvaluation.Models;
using System.Collections.ObjectModel;
using System.IO;

namespace RagEvaluation.Desktop.ViewModels;

public sealed partial class DocumentsViewModel : OperationViewModelBase
{
    private static readonly string[] FallbackEmbeddingModels =
    [
        "nomic-embed-text:latest",
        "mxbai-embed-large:latest"
    ];

    private readonly DocumentsHandlingService _documentsHandlingService;
    private readonly IDocumentRepository _documentRepository;
    private readonly IExperimentRepository _experimentRepository;
    private readonly IConfigurationRepository _configurationRepository;
    private readonly IModelRepository _modelRepository;
    private readonly ShellViewModel _shell;
    private readonly ILogger<DocumentsViewModel> _logger;

    public DocumentsViewModel(
        DocumentsHandlingService documentsHandlingService,
        IDocumentRepository documentRepository,
        IExperimentRepository experimentRepository,
        IConfigurationRepository configurationRepository,
        IModelRepository modelRepository,
        ShellViewModel shell,
        ILogger<DocumentsViewModel> logger)
        : base(logger)
    {
        _documentsHandlingService = documentsHandlingService;
        _documentRepository = documentRepository;
        _experimentRepository = experimentRepository;
        _configurationRepository = configurationRepository;
        _modelRepository = modelRepository;
        _shell = shell;
        _logger = logger;
    }

    public ObservableCollection<DocumentRow> Documents { get; } = new();
    public ObservableCollection<ChunkRow> Chunks { get; } = new();
    public ObservableCollection<string> EmbeddingModels { get; } = new();

    public int DefaultChunkSize { get; private set; } = 1200;
    public int DefaultChunkOverlap { get; private set; } = 200;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanActOnSelection))]
    private DocumentRow? _selectedDocument;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanActOnChunk))]
    private ChunkRow? _selectedChunk;

    [ObservableProperty]
    private string _selectedSourceText = string.Empty;

    [ObservableProperty]
    private string? _selectedEmbeddingModel;

    [ObservableProperty]
    private string _chunkingSummary = string.Empty;

    public bool CanActOnSelection => !IsBusy && SelectedDocument is not null;
    public bool CanActOnChunk => !IsBusy && SelectedChunk is not null;

    protected override void OnBusyChanged()
    {
        OnPropertyChanged(nameof(CanActOnSelection));
        OnPropertyChanged(nameof(CanActOnChunk));
    }

    protected override async Task OnOperationFinishedAsync()
    {
        await ReloadAsync();
        await _shell.RefreshAsync();
    }

    partial void OnSelectedDocumentChanged(DocumentRow? value) => _ = LoadSelectedDocumentAsync(value);

    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        await LoadModelsAndSettingsAsync();
        await ReloadAsync();
    }

    private async Task LoadModelsAndSettingsAsync()
    {
        try
        {
            var available = await _modelRepository.GetEmbeddingModelsAsync(CancellationToken.None);
            var models = available.Count > 0 ? available.ToArray() : FallbackEmbeddingModels;

            EmbeddingModels.Clear();
            foreach (var model in models)
            {
                EmbeddingModels.Add(model);
            }

            var settings = await _configurationRepository.GetChunkingSettingsAsync(CancellationToken.None);
            DefaultChunkSize = settings.ChunkTargetSizeChars > 0 ? settings.ChunkTargetSizeChars : 1200;
            DefaultChunkOverlap = settings.ChunkOverlapChars >= 0 ? settings.ChunkOverlapChars : 200;

            if (SelectedEmbeddingModel is null || !EmbeddingModels.Contains(SelectedEmbeddingModel))
            {
                SelectedEmbeddingModel = models.Contains(settings.EmbeddingModelName)
                    ? settings.EmbeddingModelName
                    : models.FirstOrDefault();
            }

            UpdateChunkingSummary();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load models and chunking settings");
            StatusText = $"Error loading settings: {ex.Message}";
        }
    }

    private void UpdateChunkingSummary()
        => ChunkingSummary = $"Chunking settings (global, used by every import and re-chunk): {DefaultChunkSize} chars, overlap {DefaultChunkOverlap}.";

    private async Task ReloadAsync(CancellationToken ct = default)
    {
        try
        {
            var previousId = SelectedDocument?.Id;
            var documents = (await _documentRepository.GetAllDocumentsAsync(ct)).OrderBy(d => d.Title).ToList();
            var rows = new List<DocumentRow>();
            foreach (var document in documents)
            {
                var chunkCount = (await _documentRepository.GetChunksByDocumentIdAsync(document.Id, ct)).Count();
                rows.Add(new DocumentRow
                {
                    Source = document,
                    Topics = string.Join(", ", document.Topics),
                    ChunkCount = chunkCount,
                    Created = $"{document.CreatedAt:yyyy-MM-dd}"
                });
            }

            Documents.Clear();
            foreach (var row in rows)
            {
                Documents.Add(row);
            }

            SelectedDocument = rows.FirstOrDefault(r => r.Id == previousId) ?? rows.FirstOrDefault();

            if (rows.Count == 0 && string.IsNullOrEmpty(StatusText))
            {
                StatusText = "No documents yet. Use Import to add markdown or text files.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load documents");
            StatusText = $"Error loading documents: {ex.Message}";
        }
    }

    private async Task LoadSelectedDocumentAsync(DocumentRow? document)
    {
        Chunks.Clear();
        SelectedChunk = null;
        SelectedSourceText = document?.Source.OriginalText ?? string.Empty;
        if (document is null)
        {
            return;
        }

        try
        {
            var chunks = (await _documentRepository.GetChunksByDocumentIdAsync(document.Id, CancellationToken.None))
                .OrderBy(c => c.ChunkIndex)
                .ToList();

            if (SelectedDocument?.Id != document.Id)
            {
                return;
            }

            foreach (var chunk in chunks)
            {
                Chunks.Add(new ChunkRow(chunk.Id, chunk.ChunkIndex, chunk.ChunkText));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load chunks for document {DocumentId}", document.Id);
            StatusText = $"Error loading chunks: {ex.Message}";
        }
    }

    public async Task<List<TopicSelectionItem>> LoadTopicsAsync()
    {
        var topics = await _documentRepository.GetAllTopicsAsync(CancellationToken.None);
        return topics
            .OrderBy(t => t.Name)
            .Select(t => new TopicSelectionItem { Id = t.Id, Name = t.Name, ParentId = t.ParentId })
            .ToList();
    }

    public async Task<(int Selected, int All)> CountTestQuestionsAsync()
    {
        try
        {
            var all = (await _experimentRepository.LoadTestQueriesAsync(CancellationToken.None)).Count();
            var selected = SelectedDocument is null
                ? 0
                : (await _experimentRepository.LoadTestQueriesAsync(SelectedDocument.Id, CancellationToken.None)).Count();
            return (selected, all);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not count test questions");
            return (-1, -1);
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        await ReloadAsync();
        await _shell.RefreshAsync();
    }

    [RelayCommand]
    private Task GenerateVectorsForSelectedAsync()
        => SelectedDocument is { } document
            ? GenerateVectorsAsync(new[] { document })
            : Task.CompletedTask;

    [RelayCommand]
    private Task GenerateVectorsForAllAsync() => GenerateVectorsAsync(Documents.ToList());

    private Task GenerateVectorsAsync(IReadOnlyList<DocumentRow> documents)
    {
        var model = SelectedEmbeddingModel;
        if (string.IsNullOrWhiteSpace(model))
        {
            StatusText = "Select an embedding model first.";
            return Task.CompletedTask;
        }

        if (documents.Count == 0)
        {
            StatusText = "There are no documents to embed.";
            return Task.CompletedTask;
        }

        return RunOperationAsync($"Generating vectors with {model}…", async ct =>
        {
            var totalChunks = 0;
            for (var i = 0; i < documents.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var document = documents[i];
                var position = i + 1;
                var progress = new Progress<(int Done, int Total)>(p =>
                {
                    ProgressMax = Math.Max(p.Total, 1);
                    ProgressValue = p.Done;
                    StatusText = $"Vectors ({model}) - document {position}/{documents.Count}: {document.Title} - chunk {p.Done}/{p.Total}";
                });

                totalChunks += await _documentsHandlingService.GenerateChunkVectorsAsync(document.Id, model, progress, ct);
            }

            return $"Generated vectors for {totalChunks} chunk(s) in {documents.Count} document(s) using {model}.";
        });
    }

    public Task ImportAsync(IReadOnlyList<string> files, IngestOptions options)
        => RunOperationAsync("Importing…", async ct =>
        {
            await SaveChunkingSettingsAsync(options, ct);

            var existingTitles = Documents.Select(d => d.Title).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var imported = 0;
            var skipped = 0;
            var chunkTotal = 0;
            var failures = new List<string>();
            ProgressMax = Math.Max(files.Count, 1);

            for (var i = 0; i < files.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var path = files[i];
                var title = Path.GetFileNameWithoutExtension(path);
                StatusText = $"Importing {i + 1}/{files.Count}: {title}";
                ProgressValue = i;

                if (!existingTitles.Add(title))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    var text = await File.ReadAllTextAsync(path, ct);
                    var type = Path.GetExtension(path).Equals(".md", StringComparison.OrdinalIgnoreCase)
                        ? DocumentType.Markdown
                        : DocumentType.PlainText;

                    var (_, chunkCount) = await _documentsHandlingService.IngestDocumentAsync(
                        title, text, options.EmbeddingModel, type, options.TopicNames, ct);
                    imported++;
                    chunkTotal += chunkCount;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Import failed for {Path}", path);
                    failures.Add($"{title}: {ex.Message}");
                }
            }

            ProgressValue = files.Count;
            var result = $"Imported {imported} document(s) with {chunkTotal} chunk(s).";
            if (skipped > 0)
            {
                result += $" Skipped {skipped} (a document with that title already exists).";
            }

            if (failures.Count > 0)
            {
                result += $" Failed {failures.Count}: {string.Join("; ", failures.Take(3))}{(failures.Count > 3 ? "; …" : string.Empty)}";
            }

            return result;
        });

    public Task RechunkAsync(IngestOptions options)
    {
        var targets = options.AllDocuments
            ? Documents.ToList()
            : SelectedDocument is { } selected ? new List<DocumentRow> { selected } : new List<DocumentRow>();

        if (targets.Count == 0)
        {
            StatusText = "Nothing to re-chunk.";
            return Task.CompletedTask;
        }

        return RunOperationAsync("Re-chunking…", async ct =>
        {
            await SaveChunkingSettingsAsync(options, ct);

            var done = 0;
            var failures = new List<string>();
            ProgressMax = targets.Count;

            for (var i = 0; i < targets.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var document = targets[i];
                StatusText = $"Re-chunking {i + 1}/{targets.Count}: {document.Title}";
                ProgressValue = i;

                var text = document.Source.OriginalText;
                if (string.IsNullOrWhiteSpace(text))
                {
                    failures.Add($"{document.Title}: source text is not available");
                    continue;
                }

                try
                {
                    await _documentsHandlingService.UpdateDocumentAsync(
                        document.Id, document.Title, options.EmbeddingModel, text,
                        options.DocumentType, document.Source.Topics.ToList(), ct);
                    done++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Re-chunk failed for document {DocumentId}", document.Id);
                    failures.Add($"{document.Title}: {ex.Message}");
                }
            }

            ProgressValue = targets.Count;
            var result = $"Re-chunked {done} document(s) with {options.ChunkSize}/{options.ChunkOverlap}. " +
                         "Regenerate vectors for other embedding models and the test set.";
            if (failures.Count > 0)
            {
                result += $" Failed {failures.Count}: {string.Join("; ", failures.Take(3))}{(failures.Count > 3 ? "; …" : string.Empty)}";
            }

            return result;
        });
    }

    private async Task SaveChunkingSettingsAsync(IngestOptions options, CancellationToken ct)
    {
        var modelId = await _modelRepository.GetOrCreateModelIdAsync(options.EmbeddingModel, ct);
        await _configurationRepository.UpsertChunkingSettingsAsync(modelId, options.ChunkSize, options.ChunkOverlap, ct);
        DefaultChunkSize = options.ChunkSize;
        DefaultChunkOverlap = options.ChunkOverlap;
        UpdateChunkingSummary();
    }

    public Task DeleteSelectedDocumentAsync()
    {
        if (SelectedDocument is not { } document)
        {
            return Task.CompletedTask;
        }

        return RunOperationAsync($"Deleting '{document.Title}'…", async ct =>
        {
            await _documentRepository.DeleteDocumentAsync(document.Id, ct);

            try
            {
                await _experimentRepository.DeleteTestQueriesByDocumentIdsAsync(new[] { document.Id }, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not remove test questions of deleted document {DocumentId}", document.Id);
                return $"Deleted '{document.Title}', but its test questions could not be removed: {ex.Message}";
            }

            return $"Deleted '{document.Title}' with its chunks and test questions.";
        });
    }

    public Task UpdateChunkTextAsync(ChunkRow chunk, string text)
        => RunOperationAsync($"Updating chunk #{chunk.Index}…", async ct =>
        {
            await _documentRepository.UpdateChunkTextAsync(chunk.Id, text, ct);
            return $"Chunk #{chunk.Index} updated. Its stored vectors still reflect the old text - " +
                   "regenerate vectors for this document.";
        });

    public Task DeleteChunkAsync(ChunkRow chunk)
        => RunOperationAsync($"Deleting chunk #{chunk.Index}…", async ct =>
        {
            await _documentRepository.DeleteChunkAsync(chunk.Id, ct);
            return $"Chunk #{chunk.Index} deleted. Test questions that expected it will no longer match.";
        });
}