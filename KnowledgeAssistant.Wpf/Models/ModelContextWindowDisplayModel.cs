using System.ComponentModel;

namespace KnowledgeAssistant.Wpf.Models;

public class ModelContextWindowDisplayModel : INotifyPropertyChanged
{
    private bool _internalUseOnly;
    private bool _canCallTools;
    private bool _isFavorite;
    private bool _isDirty;
    private bool _isSaving;
    private string? _saveError;

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public long Size { get; set; }

    public string SizeDisplay => FormatSize(Size);

    public int? ContextLength { get; set; }

    public string? Family { get; set; }

    public string? QuantizationLevel { get; set; }

    public string? ParameterSize { get; set; }

    public bool InternalUseOnly
    {
        get => _internalUseOnly;
        set
        {
            if (_internalUseOnly != value)
            {
                _internalUseOnly = value;
                OnPropertyChanged(nameof(InternalUseOnly));
                IsDirty = true;
            }
        }
    }

    public bool CanCallTools
    {
        get => _canCallTools;
        set
        {
            if (_canCallTools != value)
            {
                _canCallTools = value;
                OnPropertyChanged(nameof(CanCallTools));
                IsDirty = true;
            }
        }
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite != value)
            {
                _isFavorite = value;
                OnPropertyChanged(nameof(IsFavorite));
                IsDirty = true;
            }
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            _isDirty = value;
            OnPropertyChanged(nameof(IsDirty));
        }
    }

    public bool IsSaving
    {
        get => _isSaving;
        set
        {
            _isSaving = value; OnPropertyChanged(nameof(IsSaving));
            OnPropertyChanged(nameof(SaveButtonText));
        }
    }

    public string SaveButtonText => IsSaving ? "Saving..." : "Save";

    public string? SaveError
    {
        get => _saveError;
        set
        {
            _saveError = value;
            OnPropertyChanged(nameof(SaveError));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }
}