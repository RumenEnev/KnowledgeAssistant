using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace RagEvaluation.Desktop.ViewModels;

public abstract partial class OperationViewModelBase : ObservableObject
{
    private readonly ILogger _logger;
    private CancellationTokenSource? _operationCts;

    protected OperationViewModelBase(ILogger logger)
    {
        _logger = logger;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private double _progressMax = 1;

    public bool IsIdle => !IsBusy;

    partial void OnIsBusyChanged(bool value) => OnBusyChanged();

    protected virtual void OnBusyChanged()
    {
    }

    protected virtual Task OnOperationFinishedAsync() => Task.CompletedTask;

    protected async Task RunOperationAsync(
        string startText,
        Func<CancellationToken, Task<string>> work,
        string cancelledText = "Cancelled.")
    {
        if (IsBusy)
        {
            return;
        }

        _operationCts = new CancellationTokenSource();
        var ct = _operationCts.Token;
        IsBusy = true;
        ProgressValue = 0;
        ProgressMax = 1;
        StatusText = startText;

        try
        {
            StatusText = await work(ct);
        }
        catch (OperationCanceledException)
        {
            StatusText = cancelledText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation failed: {Operation}", startText);
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            try
            {
                await OnOperationFinishedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reload after operation failed");
            }
            finally
            {
                _operationCts?.Dispose();
                _operationCts = null;
                IsBusy = false;
            }
        }
    }

    [RelayCommand]
    private void CancelOperation() => _operationCts?.Cancel();
}