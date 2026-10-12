using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnowledgeAssistant.Application.Abstraction;
using Microsoft.Extensions.Logging;
using RagEvaluation.Desktop.Services;
using RagEvaluation.Interfaces;
using RagEvaluation.Models;
using RagEvaluation.Services;
using System.Collections.ObjectModel;
using System.Text;

namespace RagEvaluation.Desktop.ViewModels;

public sealed partial class TestSetViewModel : OperationViewModelBase
{
    private readonly TestSetGenerationService _generationService;
    private readonly IExperimentRepository _experimentRepository;
    private readonly IDocumentRepository _documentRepository;
    private readonly LlmCatalogService _llm;
    private readonly ShellViewModel _shell;
    private readonly ILogger<TestSetViewModel> _logger;

    private List<TestQuestionRow> _allRows = new();
    private IReadOnlyList<TestQuestionRow> _selected = Array.Empty<TestQuestionRow>();
    private int _orphanCount;

    public TestSetViewModel(
        TestSetGenerationService generationService,
        IExperimentRepository experimentRepository,
        IDocumentRepository documentRepository,
        LlmCatalogService llm,
        ShellViewModel shell,
        ILogger<TestSetViewModel> logger)
        : base(logger)
    {
        _generationService = generationService;
        _experimentRepository = experimentRepository;
        _documentRepository = documentRepository;
        _llm = llm;
        _shell = shell;
        _logger = logger;
    }

    public ObservableCollection<TestQuestionRow> Questions { get; } = new();
    public ObservableCollection<DocumentChoice> DocumentFilters { get; } = new();

    [ObservableProperty]
    private DocumentChoice? _selectedFilter;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private string _selectedQuestionText = string.Empty;

    [ObservableProperty]
    private string _expectedPassages = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDeleteSelection))]
    private int _selectedCount;

    public bool CanDeleteSelection => IsIdle && SelectedCount > 0;
    public bool CanDeleteOrphans => IsIdle && _orphanCount > 0;

    private TestQuestionRow? PrimaryQuestion => _selected.FirstOrDefault();

    partial void OnSelectedFilterChanged(DocumentChoice? value) => ApplyFilter();

    protected override void OnBusyChanged()
    {
        OnPropertyChanged(nameof(CanDeleteSelection));
        OnPropertyChanged(nameof(CanDeleteOrphans));
    }

    protected override async Task OnOperationFinishedAsync()
    {
        await ReloadAsync();
        await _shell.RefreshAsync();
    }

    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        await ReloadAsync();
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

    private async Task ReloadAsync()
    {
        try
        {
            var rows = new List<TestQuestionRow>();
            var queries = (await _experimentRepository.LoadTestQueriesAsync(CancellationToken.None)).ToList();
            var topicNameById = (await _documentRepository.GetAllTopicsAsync(CancellationToken.None))
                .ToDictionary(t => t.Id, t => t.Name);
            var titleByDocumentId = (await _documentRepository.GetAllDocumentsAsync(CancellationToken.None))
                .ToDictionary(d => d.Id, d => d.Title);

            foreach (var q in queries)
            {
                var expected = q.ExpectedChunkIds is null ? new List<int>() : q.ExpectedChunkIds.ToList();
                int? documentId = q.SourceDocumentId;
                var generator = string.Join("/", new[] { q.GeneratorProvider, q.GeneratorModel }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));

                rows.Add(new TestQuestionRow
                {
                    Id = q.Id,
                    Question = q.QueryText,
                    DocumentId = documentId,
                    Document = documentId is { } id && titleByDocumentId.TryGetValue(id, out var title) ? title : "(deleted)",
                    Topic = topicNameById.TryGetValue(q.TopicId, out var topic) ? topic : "(deleted)",
                    Generator = generator,
                    ExpectedChunkIds = expected
                });
            }

            var existing = new HashSet<int>();
            foreach (var batch in rows.SelectMany(r => r.ExpectedChunkIds).Distinct().Chunk(500))
            {
                foreach (var chunk in await _documentRepository.GetChunksByIdsAsync(batch.ToList(), CancellationToken.None))
                {
                    existing.Add(chunk.Id);
                }
            }

            foreach (var row in rows)
            {
                row.IsOrphaned = row.ExpectedChunkIds.Count > 0 && row.ExpectedChunkIds.Any(id => !existing.Contains(id));
            }

            _allRows = rows;
            _orphanCount = rows.Count(r => r.IsOrphaned);
            OnPropertyChanged(nameof(CanDeleteOrphans));

            var previousFilterId = SelectedFilter?.Id;
            DocumentFilters.Clear();
            DocumentFilters.Add(new DocumentChoice(null, "All documents"));
            foreach (var group in rows.Where(r => r.DocumentId is not null)
                         .GroupBy(r => r.DocumentId!.Value)
                         .OrderBy(g => g.First().Document))
                        {
                            DocumentFilters.Add(new DocumentChoice(group.Key, group.First().Document));
                        }

            SelectedFilter = DocumentFilters.FirstOrDefault(f => f.Id == previousFilterId) ?? DocumentFilters[0];
            ApplyFilter();

            var documents = rows.Select(r => r.DocumentId).Distinct().Count();
            var topics = rows.Select(r => r.Topic).Distinct().Count();
            SummaryText = rows.Count == 0
                ? "No test questions yet. Use Generate to create them from your chunks."
                : $"{rows.Count} question(s) · {documents} document(s) · {topics} topic(s)" +
                  (_orphanCount > 0 ? $" · {_orphanCount} orphaned (their expected chunk no longer exists)" : string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load the test set");
            StatusText = $"Error loading test set: {ex.Message}";
        }
    }

    private void ApplyFilter()
    {
        var filterId = SelectedFilter?.Id;
        Questions.Clear();
        foreach (var row in _allRows.Where(r => filterId is null || r.DocumentId == filterId))
        {
            Questions.Add(row);
        }
    }

    public void UpdateSelection(IReadOnlyList<TestQuestionRow> selected)
    {
        _selected = selected;
        SelectedCount = selected.Count;

        var primary = PrimaryQuestion;
        SelectedQuestionText = selected.Count switch
        {
            0 => string.Empty,
            1 => primary!.Question,
            _ => $"{selected.Count} questions selected. Showing the first:\n\n{primary!.Question}"
        };

        _ = LoadPassagesAsync(primary);
    }

    private async Task LoadPassagesAsync(TestQuestionRow? row)
    {
        if (row is null)
        {
            ExpectedPassages = string.Empty;
            return;
        }

        if (row.ExpectedChunkIds.Count == 0)
        {
            ExpectedPassages = "No expected chunk is recorded for this question.";
            return;
        }

        try
        {
            var chunks = (await _documentRepository.GetChunksByIdsAsync(row.ExpectedChunkIds.ToList(), CancellationToken.None))
                .OrderBy(c => c.ChunkIndex)
                .ToList();

            if (!ReferenceEquals(PrimaryQuestion, row))
            {
                return;
            }

            var sb = new StringBuilder();
            foreach (var chunk in chunks)
            {
                sb.AppendLine($"Chunk #{chunk.ChunkIndex} (id {chunk.Id})");
                sb.AppendLine();
                sb.AppendLine(chunk.ChunkText);
                sb.AppendLine();
            }

            var missing = row.ExpectedChunkIds.Except(chunks.Select(c => c.Id)).ToList();
            if (missing.Count > 0)
            {
                sb.AppendLine($"⚠ Expected chunk(s) no longer exist: {string.Join(", ", missing)}.");
                sb.AppendLine("The document was probably re-chunked. This question can no longer be scored; regenerate the test set for its document.");
            }

            ExpectedPassages = sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load expected passage for question {QuestionId}", row.Id);
            ExpectedPassages = $"Error loading the expected passage: {ex.Message}";
        }
    }

    public async Task<List<DocumentChoice>> LoadDocumentChoicesAsync()
    {
        var documents = await _documentRepository.GetAllDocumentsAsync(CancellationToken.None);
        var choices = new List<DocumentChoice> { new(null, "All documents") };
        choices.AddRange(documents.OrderBy(d => d.Title).Select(d => new DocumentChoice(d.Id, d.Title)));
        return choices;
    }

    public Task GenerateAsync(GenerateTestSetOptions options)
        => RunOperationAsync(
            "Generating test questions…",
            async ct =>
            {
                var gateway = _llm.GetGateway(options.Provider);
                var progress = new Progress<(int done, int total)>(p =>
                {
                    ProgressMax = Math.Max(p.total, 1);
                    ProgressValue = p.done;
                    StatusText = $"Generating questions: chunk {p.done}/{p.total}";
                });

                var saved = await _generationService.GenerateAsync(
                    gateway, options.Provider, options.Model, options.QuestionsPerChunk,
                    options.DocumentId, progress, ct, replaceExisting: !options.SkipExisting);

                return saved == 0
                    ? "No questions were saved: no eligible documents, nothing to do, or the model returned nothing usable."
                    : $"Saved {saved} question(s) using {options.Provider} / {options.Model}.";
            },
            cancelledText: "Cancelled. Documents finished before cancelling keep their new questions; the others are unchanged.");

    public Task DeleteSelectedAsync()
    {
        var ids = _selected.Select(q => q.Id).ToList();
        return DeleteAsync(ids, $"Deleted {ids.Count} question(s).");
    }

    public Task DeleteOrphansAsync()
    {
        var ids = _allRows.Where(r => r.IsOrphaned).Select(r => r.Id).ToList();
        return DeleteAsync(ids, $"Deleted {ids.Count} orphaned question(s).");
    }

    public int OrphanCount => _orphanCount;

    private Task DeleteAsync(List<int> ids, string doneText)
    {
        if (ids.Count == 0)
        {
            return Task.CompletedTask;
        }

        return RunOperationAsync($"Deleting {ids.Count} question(s)…", async ct =>
        {
            await _experimentRepository.DeleteTestQueriesAsync(ids, ct);
            return doneText;
        });
    }
}