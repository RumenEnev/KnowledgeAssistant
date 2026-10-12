using Microsoft.Extensions.Logging;
using RagEvaluation.Models;
using RagEvaluation.Services;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace RagEvaluation.Desktop.ViewModels;

public sealed class ShellViewModel : INotifyPropertyChanged
{
    private readonly PipelineStatusService _statusService;
    private readonly ILogger<ShellViewModel> _logger;
    private CancellationTokenSource? _cts;
    private IReadOnlyList<StatusItem> _items = Array.Empty<StatusItem>();
    private bool _isRefreshing;

    public ShellViewModel(PipelineStatusService statusService, ILogger<ShellViewModel> logger)
    {
        _statusService = statusService;
        _logger = logger;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<StatusItem> Items
    {
        get => _items;
        private set => Set(ref _items, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set => Set(ref _isRefreshing, value);
    }

    public async Task RefreshAsync()
    {
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        IsRefreshing = true;

        try
        {
            var status = await _statusService.GetAsync(cts.Token);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            Items = BuildItems(status);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer refresh.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh pipeline status");
            if (!cts.IsCancellationRequested)
            {
                Items = new[] { new StatusItem("Status", "unavailable", ex.Message) };
            }
        }
        finally
        {
            if (ReferenceEquals(_cts, cts))
            {
                IsRefreshing = false;
            }
        }
    }

    private static IReadOnlyList<StatusItem> BuildItems(PipelineStatus status)
    {
        var lastRun = status.LastRunName is null
            ? "none yet"
            : status.LastRunRecall is { } recall
                ? $"{status.LastRunName} · Recall {PipelineStatusService.FormatRecall(recall)} · {status.LastRunWhen}"
                : $"{status.LastRunName} · {status.LastRunWhen}";

        return new[]
        {
            new StatusItem("Documents", status.Documents.ToString("N0", CultureInfo.CurrentCulture)),
            new StatusItem("Chunks", status.Chunks.ToString("N0", CultureInfo.CurrentCulture)),
            new StatusItem("Test questions", status.TestQuestions.ToString("N0", CultureInfo.CurrentCulture)),
            new StatusItem("Last run", lastRun)
        };
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}