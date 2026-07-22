using System.ComponentModel;

namespace KnowledgeAssistant.Wpf.Models
{
    /// <summary>A model shown in the Model Context Windows window with an editable context window token count.</summary>
    public class ModelContextWindowDisplayModel : INotifyPropertyChanged
    {
        private int? _contextWindowTokens;
        private bool _isSaving;

        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public int? ContextWindowTokens
        {
            get => _contextWindowTokens;
            set { _contextWindowTokens = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContextWindowTokens))); }
        }

        public bool IsSaving
        {
            get => _isSaving;
            set
            {
                _isSaving = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSaving)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SaveButtonText)));
            }
        }

        public string SaveButtonText => IsSaving ? "Saving..." : "Save";

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
